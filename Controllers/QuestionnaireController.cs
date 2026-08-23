using FenixLegalOs.Data;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionnaireController : ControllerBase
{
    [HttpGet]
    public IActionResult GetQuestionnaire()
    {
        return Ok(new
        {
            sections = DataBank.Sections,
            questions = DataBank.Questions.Where(q => q.Enabled),
            versions = new
            {
                questionBank = DataBank.QuestionBankVersion,
                scoringEngine = DataBank.ScoringEngineVersion,
                riskLibrary = DataBank.RiskLibraryVersion
            }
        });
    }
}
