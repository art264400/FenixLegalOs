using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Core;

/// <summary>
/// Validates routing dependency authority, topological order, and acyclicity of ShowIf rules.
/// Ensures that no question can depend on itself, a downstream question, or create dependency cycles.
/// </summary>
public static class RoutingDependencyValidator
{
    public static void Validate(IReadOnlyList<DiagnosticQuestion> questions)
    {
        var questionMap = questions.ToDictionary(q => q.Id, q => q, StringComparer.Ordinal);
        var orderMap = questions.ToDictionary(q => q.Id, q => q.Order, StringComparer.Ordinal);

        // 1. Discover fact producers by evaluating each question independently
        var factToProducerMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var q in questions)
        {
            if (q.Options == null || q.Options.Count == 0) continue;

            // Test options
            foreach (var opt in q.Options)
            {
                var testAnswers = new Dictionary<string, object>
                {
                    [q.Id] = q.Type == QuestionType.Multiple ? new List<string> { opt.Id } : opt.Id
                };
                var facts = FactNormalizer.NormalizeFacts(testAnswers);
                foreach (var factKey in facts.Facts.Keys)
                {
                    if (factKey == "diagnostic.unknownQuestionIds") continue;
                    if (!factToProducerMap.ContainsKey(factKey))
                    {
                        factToProducerMap[factKey] = q.Id;
                    }
                }
            }
        }

        // 2. Build dependency graph and validate order
        var adj = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var q in questions)
        {
            adj[q.Id] = new HashSet<string>(StringComparer.Ordinal);
            var refKeys = ExtractReferencedKeys(q.ShowIf);

            foreach (var key in refKeys)
            {
                string producerQuestionId;
                if (questionMap.ContainsKey(key))
                {
                    producerQuestionId = key;
                }
                else if (factToProducerMap.TryGetValue(key, out var prodId))
                {
                    producerQuestionId = prodId;
                }
                else
                {
                    // If fact is not recognized and not a question ID, fail closed
                    throw new InvalidOperationException(
                        $"Routing validation failure: Question '{q.Id}' ShowIf references unknown fact or question key '{key}'.");
                }

                // A. Check self-dependency
                if (producerQuestionId == q.Id)
                {
                    throw new InvalidOperationException(
                        $"Routing validation failure: Self-dependency detected in question '{q.Id}' (depends on key '{key}' produced by itself).");
                }

                // B. Check order: producer must strictly precede dependent in canonical order
                if (orderMap.TryGetValue(producerQuestionId, out var prodOrder) &&
                    orderMap.TryGetValue(q.Id, out var currentOrder))
                {
                    if (prodOrder >= currentOrder)
                    {
                        throw new InvalidOperationException(
                            $"Routing validation failure: Forward/backwards dependency detected. Question '{q.Id}' (Order {currentOrder}) depends on question '{producerQuestionId}' (Order {prodOrder}) via key '{key}'.");
                    }
                }

                adj[q.Id].Add(producerQuestionId);
            }
        }

        // 3. Cycle detection via DFS
        var visited = new Dictionary<string, int>(); // 0=unvisited, 1=visiting (gray), 2=visited (black)
        foreach (var q in questions) visited[q.Id] = 0;

        foreach (var q in questions)
        {
            if (visited[q.Id] == 0)
            {
                if (HasCycle(q.Id, adj, visited))
                {
                    throw new InvalidOperationException(
                        $"Routing validation failure: Dependency cycle detected involving question '{q.Id}'.");
                }
            }
        }
    }

    private static bool HasCycle(string node, Dictionary<string, HashSet<string>> adj, Dictionary<string, int> visited)
    {
        visited[node] = 1; // gray

        if (adj.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (visited.TryGetValue(neighbor, out var state))
                {
                    if (state == 1) return true; // cycle found
                    if (state == 0 && HasCycle(neighbor, adj, visited)) return true;
                }
            }
        }

        visited[node] = 2; // black
        return false;
    }

    public static List<string> ExtractReferencedKeys(List<ConditionalRule>? rules)
    {
        var keys = new List<string>();
        if (rules == null) return keys;

        foreach (var r in rules)
        {
            if (!string.IsNullOrEmpty(r.QuestionId))
            {
                keys.Add(r.QuestionId);
            }
            if (r.All != null) keys.AddRange(ExtractReferencedKeys(r.All));
            if (r.Any != null) keys.AddRange(ExtractReferencedKeys(r.Any));
        }

        return keys.Distinct().ToList();
    }
}
