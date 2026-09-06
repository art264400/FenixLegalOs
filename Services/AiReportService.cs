using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using Microsoft.Extensions.Configuration;

namespace FenixLegalOs.Services;

public class NarrativeGenerationOptions
{
    public int MaxParallelModuleRequests { get; set; } = 4;
    public int ActionBatchSize { get; set; } = 5;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public bool EnableMetricsLogging { get; set; } = true;
}

public class AiReportService
{
    private readonly ILlmNarrativeClient _llmClient;
    private readonly NarrativeGenerationOptions _options;
    private readonly ConcurrentBag<NarrativeGenerationMetrics> _metrics = new();
    private readonly bool _strictLlm;

    public NarrativeGenerationOptions Options => _options;
    public IReadOnlyList<NarrativeGenerationMetrics> LastMetrics => _metrics.ToList();
    public bool StrictLlm => _strictLlm;

    public AiReportService(IConfiguration? config = null, ILlmNarrativeClient? llmClient = null)
    {
        _options = new NarrativeGenerationOptions();

        if (config != null)
        {
            if (int.TryParse(config["NarrativeGeneration:MaxParallelModuleRequests"], out var p) && p > 0)
                _options.MaxParallelModuleRequests = p;
            if (int.TryParse(config["NarrativeGeneration:ActionBatchSize"], out var b) && b > 0)
                _options.ActionBatchSize = b;
            if (int.TryParse(config["NarrativeGeneration:RequestTimeoutSeconds"], out var t) && t > 0)
                _options.RequestTimeoutSeconds = t;
        }

        var strictEnv = Environment.GetEnvironmentVariable("STRICT_LLM");
        if (!string.IsNullOrWhiteSpace(strictEnv) && bool.TryParse(strictEnv, out var strictFromEnv))
        {
            _strictLlm = strictFromEnv;
        }
        else if (config != null && bool.TryParse(config["AiSettings:StrictLlm"], out var strictFromConfig))
        {
            _strictLlm = strictFromConfig;
        }
        else
        {
            _strictLlm = false;
        }

        _llmClient = llmClient ?? new LlmNarrativeClient(config);

        Console.WriteLine($"[AiReportService] Initialized with Chunked Pipeline. Concurrency: {_options.MaxParallelModuleRequests}, BatchSize: {_options.ActionBatchSize}, Timeout: {_options.RequestTimeoutSeconds}s, Configured: {_llmClient.IsConfigured}, StrictLlm: {_strictLlm}");
    }

    public async Task<ReportNarrativesDto> GenerateReportNarrativesAsync(ReportContext context, CancellationToken ct = default)
    {
        // 0. Ensure no mutation of source ReportContext
        var expectedInitialFingerprint = ReportQualityGate.ComputeContextFingerprint(context);

        if (!_llmClient.IsConfigured)
        {
            if (_strictLlm)
            {
                throw new InvalidOperationException("LLM is required (StrictLlm=true), but LLM API key is not configured.");
            }

            Console.WriteLine("[AiReportService] LLM not configured -> Generating full granular deterministic fallback narratives.");
            var fallback = DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
            fallback.IsReady = false;
            fallback.FailedBlocks = new List<string> { "LLM API Key not configured" };
            return fallback;
        }

        try
        {
            // 1. Prepare minimal shared project context
            var sharedContext = ContextSelector.ExtractCompactSharedContext(context);

            // 2. Parallel Module Generation (Bounded Concurrency via SemaphoreSlim)
            // CRITICAL: Only applicable modules are processed. N/A modules generate 0 LLM calls!
            var applicableModules = context.FocusModules
                .Where(m => !context.NotApplicableModules.Any(na => string.Equals(na.SectionId, m.SectionId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var moduleNarratives = new ConcurrentDictionary<string, ModuleNarrativeDto>(StringComparer.OrdinalIgnoreCase);
            var moduleFingerprints = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var semaphore = new SemaphoreSlim(_options.MaxParallelModuleRequests, _options.MaxParallelModuleRequests);

            var moduleTasks = applicableModules.Select(async module =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var crossModuleSignals = ContextSelector.SelectCrossModuleSignals(context, module.SectionId);

                    var moduleRequest = new ModuleNarrativeRequestDto
                    {
                        ProjectContext = sharedContext,
                        CrossModuleSignals = crossModuleSignals,
                        Module = new ModuleNarrativeInputDto
                        {
                            SectionId = module.SectionId,
                            Title = module.Title,
                            Score = module.Score,
                            Band = module.ScoreBand,
                            MaxSeverity = module.MaxSeverity.ToString(),
                            Findings = module.Findings.Select(f => new ModuleFindingInputDto
                            {
                                FindingCode = f.FindingCode,
                                RootCauseCode = !string.IsNullOrWhiteSpace(f.FindingCode) ? f.FindingCode : string.Empty,
                                Title = f.Title,
                                Severity = f.Severity.ToString(),
                                Priority = f.Priority.ToString(),
                                WhyFound = f.WhyFound,
                                WhyItMatters = f.WhyItMatters,
                                Recommendation = (f.Recommendations != null && f.Recommendations.Count > 0) ? f.Recommendations.First() : f.Recommendation,
                                Recommendations = (f.Recommendations != null && f.Recommendations.Count > 0)
                                    ? f.Recommendations.Take(3).ToList()
                                    : (!string.IsNullOrWhiteSpace(f.Recommendation) ? new List<string> { f.Recommendation } : new List<string>())
                            }).ToList()
                        }
                    };

                    var (rawModuleResponse, metrics) = await _llmClient.GenerateModuleNarrativeAsync(moduleRequest, ct);
                    _metrics.Add(metrics);

                    var (isValid, sanitizedModule, error) = ChunkedNarrativeValidator.ValidateAndSanitizeModule(
                        rawModuleResponse, module, context);

                    if (!isValid)
                    {
                        metrics.FallbackUsed = true;
                        metrics.ErrorOrValidationFailure = error;
                        Console.WriteLine($"[AiReportService] Module '{module.SectionId}' validation failed: {error}");
                        if (_strictLlm)
                        {
                            throw new InvalidOperationException($"LLM generation failed for module '{module.SectionId}': {error}");
                        }
                        Console.WriteLine($"[AiReportService] Using granular module fallback for '{module.SectionId}'.");
                    }

                    moduleNarratives[module.SectionId] = sanitizedModule;
                    moduleFingerprints[module.SectionId] = ComputeModuleFingerprint(module);
                }
                catch (Exception ex) when (!_strictLlm)
                {
                    Console.WriteLine($"[AiReportService] Exception generating module '{module.SectionId}': {ex.Message} -> using granular module fallback.");
                    var fallbackModule = DeterministicFallbackNarratives.GenerateModuleFallback(context, module.SectionId);
                    moduleNarratives[module.SectionId] = fallbackModule;
                    moduleFingerprints[module.SectionId] = ComputeModuleFingerprint(module);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(moduleTasks);

            // 3. Batched Action Narrative Generation
            var actionNarratives = new ConcurrentDictionary<string, ActionNarrativeItemDto>(StringComparer.OrdinalIgnoreCase);
            var actionBatchFingerprints = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (context.ActionPlan.Count > 0)
            {
                var actionBatches = context.ActionPlan
                    .Select((action, index) => new { action, index })
                    .GroupBy(x => x.index / _options.ActionBatchSize)
                    .Select(g => g.Select(x => x.action).ToList())
                    .ToList();

                foreach (var batch in actionBatches)
                {
                    var batchKey = string.Join("_", batch.Select(a => a.ActionId));
                    try
                    {
                        var batchRequest = new ActionBatchRequestDto
                        {
                            ProjectContext = sharedContext,
                            Actions = batch.Select(a => new ActionNarrativeInputDto
                            {
                                ActionId = a.ActionId,
                                Title = a.Title,
                                WhatToDo = a.WhatToDo,
                                PriorityGroup = a.PriorityGroup,
                                ResolutionMode = a.ResolutionMode.ToString(),
                                SourceFindings = a.CoveredFindingCodes.Select(code =>
                                {
                                    var f = context.AllFindings.FirstOrDefault(x => x.Code == code);
                                    return new ActionSourceFindingDto
                                    {
                                        FindingCode = code,
                                        Severity = f?.Severity.ToString() ?? "High",
                                        WhyFound = f?.Finding ?? string.Empty
                                    };
                                }).ToList()
                            }).ToList()
                        };

                        var (rawActionResponse, metrics) = await _llmClient.GenerateActionBatchNarrativeAsync(batchRequest, ct);
                        _metrics.Add(metrics);

                        var (isValid, sanitizedBatch, error) = ChunkedNarrativeValidator.ValidateAndSanitizeActionBatch(
                            rawActionResponse, batch, context);

                        if (!isValid)
                        {
                            metrics.FallbackUsed = true;
                            metrics.ErrorOrValidationFailure = error;
                            Console.WriteLine($"[AiReportService] Action batch '{batchKey}' validation failed: {error}");
                            if (_strictLlm)
                            {
                                throw new InvalidOperationException($"LLM generation failed for action batch '{batchKey}': {error}");
                            }
                            Console.WriteLine($"[AiReportService] Using granular batch fallback for '{batchKey}'.");
                        }

                        foreach (var (k, v) in sanitizedBatch)
                        {
                            actionNarratives[k] = v;
                        }

                        actionBatchFingerprints[batchKey] = ComputeBatchFingerprint(batch);
                    }
                    catch (Exception ex) when (!_strictLlm)
                    {
                        Console.WriteLine($"[AiReportService] Exception generating action batch '{batchKey}': {ex.Message} -> using granular batch fallback.");
                        var fallbackBatch = DeterministicFallbackNarratives.GenerateActionBatchFallback(context, batch.Select(a => a.ActionId));
                        foreach (var (k, v) in fallbackBatch)
                        {
                            actionNarratives[k] = v;
                        }
                        actionBatchFingerprints[batchKey] = ComputeBatchFingerprint(batch);
                    }
                }
            }

            // 4. Executive Synthesis (STRICTLY AFTER module & action generation)
            // Synthesize from compact inputs and the newly generated module summaries!
            ExecutiveSynthesisResponseDto finalExecSynthesis;
            try
            {
                var moduleSummariesForExecutive = applicableModules.Select(m => new ModuleExecutiveSummaryDto
                {
                    SectionId = m.SectionId,
                    Title = m.Title,
                    Score = m.Score,
                    MaxSeverity = m.MaxSeverity.ToString(),
                    Summary = moduleNarratives.TryGetValue(m.SectionId, out var mn) ? mn.Summary : string.Empty
                }).ToList();

                var execRequest = new ExecutiveSynthesisRequestDto
                {
                    ProjectProfile = new CompactProjectProfileExecutiveDto
                    {
                        Jurisdiction = sharedContext.Jurisdiction,
                        EntityStatus = sharedContext.EntityStatus,
                        Founders = sharedContext.Founders,
                        Ownership = sharedContext.Ownership,
                        ProductStage = sharedContext.ProductStage,
                        ProductCreators = sharedContext.ProductCreators,
                        Users = sharedContext.Users,
                        Fundraising = sharedContext.Fundraising
                    },
                    OverallScore = context.Overall?.Score ?? 0,
                    OverallBand = context.Overall?.Band ?? string.Empty,
                    OverallLevelTitle = context.Overall?.LevelTitle ?? string.Empty,
                    OverallConfidence = context.Overall?.Confidence ?? 0,
                    RiskCounts = new Dictionary<string, int>
                    {
                        ["blocker"] = context.AllFindings.Count(f => f.Severity == RiskSeverity.Blocker),
                        ["critical"] = context.AllFindings.Count(f => f.Severity == RiskSeverity.Critical),
                        ["high"] = context.AllFindings.Count(f => f.Severity == RiskSeverity.High)
                    },
                    TopFindings = context.TopFindings.Take(5).Select(t => new TopFindingExecutiveSummaryDto
                    {
                        FindingCode = t.FindingCode,
                        RootCauseCode = t.RootCauseCode,
                        Title = t.Title,
                        Severity = t.Severity.ToString(),
                        Summary = t.ShortSummary
                    }).ToList(),
                    ModuleSummaries = moduleSummariesForExecutive,
                    TopActions = context.ActionPlan.Take(3).Select(a => a.Title).ToList(),
                    PositiveFactors = context.PositiveFactors.Take(3).Select(p => p.Title).ToList(),
                    InvestmentReadiness = context.InvestmentReadiness != null ? new CompactInvestmentReadinessDto
                    {
                        IsApplicable = context.InvestmentReadiness.IsApplicable,
                        ReadinessScore = context.InvestmentReadiness.ReadinessScore,
                        Category = context.InvestmentReadiness.Category,
                        UnresolvedBlockersCount = context.InvestmentReadiness.UnresolvedBlockersCount,
                        BlockerTitles = context.InvestmentReadiness.BlockerTitles.Take(3).ToList()
                    } : null,
                    RequiresLegalWork = context.FenixLaw?.RequiresLegalWork ?? false,
                    FenixLawServiceAreas = context.FenixLaw?.ServiceAreas ?? new List<string>()
                };

                var (rawExecResponse, execMetrics) = await _llmClient.GenerateExecutiveSynthesisAsync(execRequest, ct);
                _metrics.Add(execMetrics);

                var (isValidExec, sanitizedExec, execError) = ChunkedNarrativeValidator.ValidateAndSanitizeExecutive(
                    rawExecResponse, context);

                if (!isValidExec)
                {
                    execMetrics.FallbackUsed = true;
                    execMetrics.ErrorOrValidationFailure = execError;
                    Console.WriteLine($"[AiReportService] Executive synthesis validation failed: {execError}");
                    if (_strictLlm)
                    {
                        throw new InvalidOperationException($"LLM generation failed for executive synthesis: {execError}");
                    }
                    Console.WriteLine($"[AiReportService] Using granular executive fallback.");
                }

                finalExecSynthesis = sanitizedExec;
            }
            catch (Exception ex) when (!_strictLlm)
            {
                Console.WriteLine($"[AiReportService] Exception generating executive synthesis: {ex.Message} -> using granular executive fallback.");
                finalExecSynthesis = DeterministicFallbackNarratives.GenerateExecutiveFallback(context, moduleNarratives.ToDictionary(k => k.Key, v => v.Value));
            }

            // 5. Assemble Final ReportNarrativesDto
            var failedBlocks = _metrics.Where(m => m.FallbackUsed || !m.Success).Select(m => $"{m.Stage}: {m.ModuleOrBatch} ({m.ErrorOrValidationFailure ?? "failure"})").ToList();
            var isAllBlocksReady = failedBlocks.Count == 0;

            var assembled = new ReportNarrativesDto
            {
                ContextFingerprint = expectedInitialFingerprint,
                SchemaVersion = "2.0",
                IsReady = isAllBlocksReady,
                FailedBlocks = failedBlocks,
                ProjectProfileNarrative = finalExecSynthesis.ProjectProfileNarrative,
                ExecutiveConclusion = finalExecSynthesis.ExecutiveConclusion,
                RootCauseSummaries = finalExecSynthesis.RootCauseSummaries,
                ModuleNarratives = moduleNarratives.ToDictionary(k => k.Key, v => v.Value),
                ActionNarratives = actionNarratives.ToDictionary(k => k.Key, v => v.Value),
                FenixLawRecommendation = finalExecSynthesis.FenixLawRecommendation
            };

            // 6. Quality Gate & Grounding verification
            var validated = ReportQualityGate.ValidateAndSanitize(assembled, context);
            validated.IsReady = isAllBlocksReady;
            validated.FailedBlocks = failedBlocks;

            // Double check: ensure ReportContext was not mutated
            var postFingerprint = ReportQualityGate.ComputeContextFingerprint(context);
            if (expectedInitialFingerprint != postFingerprint)
            {
                throw new InvalidOperationException("CRITICAL INVARIANT VIOLATION: ReportContext was mutated during narrative generation!");
            }

            return validated;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AiReportService] Pipeline failure: {ex.Message}");
            if (_strictLlm)
            {
                throw;
            }

            Console.WriteLine($"[AiReportService] Using fallback narratives due to: {ex.Message}");
            var fallback = DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
            fallback.IsReady = false;
            fallback.FailedBlocks = new List<string> { $"Pipeline failure: {ex.Message}" };
            return fallback;
        }
    }

    private static string ComputeModuleFingerprint(FocusModuleDetailDto module)
    {
        var raw = $"{module.SectionId}|{module.Score}|{module.MaxSeverity}|{string.Join(",", module.Findings.Select(f => f.FindingCode))}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16];
    }

    private static string ComputeBatchFingerprint(List<UnifiedActionItemDto> batch)
    {
        var raw = string.Join(",", batch.Select(a => $"{a.ActionId}_{a.PriorityGroup}_{a.ResolutionMode}"));
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16];
    }
}
