using Xunit;

namespace DamControlSystem.Tests;

public class ReportGenerationTest
{
    // Generates the comprehensive STQA HTML Requirement Matrix report upon test suite execution
    [Fact]
    public void Generate_StqaHtmlReport_WithRequirements()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        string? solutionDir = null;

        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DamControlSystem.sln")))
            {
                solutionDir = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        if (solutionDir != null)
        {
            var testResultsDir = Path.Combine(solutionDir, "tests", "DamControlSystem.Tests", "TestResults");
            Directory.CreateDirectory(testResultsDir);
            var trxPath = Path.Combine(testResultsDir, "test_results.trx");
            var outputHtmlRoot = Path.Combine(solutionDir, "TestReport.html");
            var outputHtmlResults = Path.Combine(testResultsDir, "TestReport.html");

            if (File.Exists(trxPath))
            {
                StqaHtmlReportGenerator.GenerateHtmlReportFromTrx(trxPath, outputHtmlRoot);
                StqaHtmlReportGenerator.GenerateHtmlReportFromTrx(trxPath, outputHtmlResults);
            }
        }
    }
}
