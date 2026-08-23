using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionnaireController : ControllerBase
{
    private readonly QuestionRepository _questionRepo;

    public QuestionnaireController(QuestionRepository questionRepo)
    {
        _questionRepo = questionRepo;
    }

    [HttpGet]
    public IActionResult GetQuestionnaire()
    {
        var sections = _questionRepo.GetSections(enabledOnly: true);
        var questions = _questionRepo.GetQuestions(enabledOnly: true);
        var versions = _questionRepo.GetVersions();

        return Ok(new
        {
            sections,
            questions,
            versions = new
            {
                questionBank = versions.GetValueOrDefault("question_bank", "1.1.0"),
                scoringEngine = versions.GetValueOrDefault("scoring_engine", "1.1.0"),
                riskLibrary = versions.GetValueOrDefault("risk_library", "1.1.0")
            }
        });
    }
}
