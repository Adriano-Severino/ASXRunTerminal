using System;
using System.Collections.Generic;
using ASXRunTerminal.Subagents;
using Xunit;

namespace ASXRunTerminal.Tests;

public class CodeReviewDataStructuresTests
{
    [Fact]
    public void CodeReviewContext_ShouldHaveExpectedDefaults()
    {
        // Arrange & Act
        var context = new CodeReviewContext();

        // Assert
        Assert.Empty(context.FilePaths);
        Assert.Empty(context.FileContents);
        Assert.Equal(CodeReviewSeverity.Low, context.MinSeverity);
        Assert.Equal(CodeReviewFocus.Comprehensive, context.Focus);
        Assert.True(context.UseRag);
        Assert.Null(context.Model);
        Assert.False(context.IncludeRefactoringSuggestions);
        Assert.True(context.CheckProjectRules);
    }

    [Fact]
    public void CodeReviewContext_ShouldAllowCustomValues()
    {
        // Arrange & Act
        var context = new CodeReviewContext
        {
            FilePaths = new[] { "/test/path.cs" },
            FileContents = new Dictionary<string, string> { { "/test/path.cs", "test content" } },
            MinSeverity = CodeReviewSeverity.High,
            Focus = CodeReviewFocus.Security,
            UseRag = false,
            Model = "custom-model",
            IncludeRefactoringSuggestions = true,
            CheckProjectRules = false
        };

        // Assert
        Assert.Single(context.FilePaths);
        Assert.Single(context.FileContents);
        Assert.Equal(CodeReviewSeverity.High, context.MinSeverity);
        Assert.Equal(CodeReviewFocus.Security, context.Focus);
        Assert.False(context.UseRag);
        Assert.Equal("custom-model", context.Model);
        Assert.True(context.IncludeRefactoringSuggestions);
        Assert.False(context.CheckProjectRules);
    }

    [Fact]
    public void CodeReviewResult_ShouldHaveExpectedDefaults()
    {
        // Arrange & Act
        var result = new CodeReviewResult();

        // Assert
        Assert.Equal(CodeReviewStatus.Completed, result.Status);
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Metrics);
        Assert.Empty(result.Recommendations);
        Assert.Null(result.RagContext);
        Assert.Equal(0, result.DurationMs);
    }

    [Fact]
    public void CodeReviewIssue_ShouldPopulateCorrectly()
    {
        // Arrange & Act
        var issue = new CodeReviewIssue
        {
            Id = "TEST-001",
            FilePath = "/test/path.cs",
            LineNumber = 42,
            Severity = CodeReviewSeverity.Critical,
            Category = "Security",
            Title = "Test Issue",
            Description = "This is a test issue",
            Suggestion = "Fix it this way",
            ExampleCode = "fixed code",
            ProjectRule = "Use implicit operator",
            Tags = new[] { "security", "critical" }
        };

        // Assert
        Assert.Equal("TEST-001", issue.Id);
        Assert.Equal("/test/path.cs", issue.FilePath);
        Assert.Equal(42, issue.LineNumber);
        Assert.Equal(CodeReviewSeverity.Critical, issue.Severity);
        Assert.Equal("Security", issue.Category);
        Assert.Equal("Test Issue", issue.Title);
        Assert.Equal("This is a test issue", issue.Description);
        Assert.Equal("Fix it this way", issue.Suggestion);
        Assert.Equal("fixed code", issue.ExampleCode);
        Assert.Equal("Use implicit operator", issue.ProjectRule);
        Assert.Equal(2, issue.Tags.Count);
    }

    [Fact]
    public void CodeReviewMetrics_ShouldCalculateCorrectly()
    {
        // Arrange & Act
        var metrics = new CodeReviewMetrics
        {
            TotalFiles = 5,
            TotalLines = 1000,
            IssuesBySeverity = new Dictionary<CodeReviewSeverity, int>
            {
                { CodeReviewSeverity.Critical, 2 },
                { CodeReviewSeverity.High, 3 },
                { CodeReviewSeverity.Low, 5 }
            },
            IssuesByCategory = new Dictionary<string, int>
            {
                { "Security", 2 },
                { "Performance", 3 },
                { "Style", 5 }
            },
            QualityScore = 85,
            ProjectRuleCompliance = 90
        };

        // Assert
        Assert.Equal(5, metrics.TotalFiles);
        Assert.Equal(1000, metrics.TotalLines);
        Assert.Equal(3, metrics.IssuesBySeverity.Count);
        Assert.Equal(2, metrics.IssuesBySeverity[CodeReviewSeverity.Critical]);
        Assert.Equal(3, metrics.IssuesByCategory.Count);
        Assert.Equal(85, metrics.QualityScore);
        Assert.Equal(90, metrics.ProjectRuleCompliance);
    }

    [Fact]
    public void RagContextInfo_ShouldPopulateCorrectly()
    {
        // Arrange & Act
        var ragInfo = new RagContextInfo
        {
            DocumentsRetrieved = 10,
            AverageSimilarity = 0.85,
            RagDurationMs = 1500,
            ContextFiles = new[] { "/file1.cs", "/file2.cs" }
        };

        // Assert
        Assert.Equal(10, ragInfo.DocumentsRetrieved);
        Assert.Equal(0.85, ragInfo.AverageSimilarity);
        Assert.Equal(1500, ragInfo.RagDurationMs);
        Assert.Equal(2, ragInfo.ContextFiles.Count);
    }

    [Fact]
    public void CodeSearchResult_ShouldPopulateCorrectly()
    {
        // Arrange & Act
        var searchResult = new CodeSearchResult
        {
            DocumentId = "doc-123",
            Content = "code content",
            FilePath = "/path/to/file.cs",
            Similarity = 0.92f
        };

        // Assert
        Assert.Equal("doc-123", searchResult.DocumentId);
        Assert.Equal("code content", searchResult.Content);
        Assert.Equal("/path/to/file.cs", searchResult.FilePath);
        Assert.Equal(0.92f, searchResult.Similarity);
    }

    [Fact]
    public void CodeReviewSeverity_ShouldHaveExpectedValues()
    {
        // Assert
        var values = Enum.GetValues<CodeReviewSeverity>();
        Assert.Equal(5, values.Length);
        Assert.Contains(CodeReviewSeverity.Critical, values);
        Assert.Contains(CodeReviewSeverity.High, values);
        Assert.Contains(CodeReviewSeverity.Medium, values);
        Assert.Contains(CodeReviewSeverity.Low, values);
        Assert.Contains(CodeReviewSeverity.Info, values);
    }

    [Fact]
    public void CodeReviewFocus_ShouldHaveExpectedValues()
    {
        // Assert
        var values = Enum.GetValues<CodeReviewFocus>();
        Assert.Equal(6, values.Length);
        Assert.Contains(CodeReviewFocus.Comprehensive, values);
        Assert.Contains(CodeReviewFocus.Security, values);
        Assert.Contains(CodeReviewFocus.Performance, values);
        Assert.Contains(CodeReviewFocus.Maintainability, values);
        Assert.Contains(CodeReviewFocus.Testing, values);
        Assert.Contains(CodeReviewFocus.ProjectRules, values);
    }
}
