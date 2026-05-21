namespace STRAIBot.Services;

public class MarkdownPropertyMemoryService : IPropertyMemoryService
{
    private readonly string _memoryBasePath;
    private readonly ILogger<MarkdownPropertyMemoryService> _logger;

    // Number of top-scoring sections to include in retrieved context
    private const int TopSectionsToReturn = 5;
    // Minimum keyword match score to include a section
    private const int MinimumScore = 1;

    public MarkdownPropertyMemoryService(
        IWebHostEnvironment env,
        ILogger<MarkdownPropertyMemoryService> logger)
    {
        _logger = logger;

        // Resolve memory/ directory relative to solution root (one level above ContentRootPath)
        var solutionRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, ".."));
        _memoryBasePath = Path.Combine(solutionRoot, "memory");

        _logger.LogInformation("Memory base path resolved to: {MemoryBasePath}", _memoryBasePath);
    }

    public bool PropertyExists(string propertyName)
    {
        var propertyPath = GetPropertyPath(propertyName);
        return Directory.Exists(propertyPath) &&
               Directory.GetFiles(propertyPath, "*.md", SearchOption.AllDirectories).Length > 0;
    }

    public async Task<string> GetRelevantContextAsync(string propertyName, string guestMessage)
    {
        var propertyPath = GetPropertyPath(propertyName);

        if (!Directory.Exists(propertyPath))
        {
            _logger.LogWarning("Property directory not found: {PropertyPath}", propertyPath);
            return string.Empty;
        }

        var mdFiles = Directory.GetFiles(propertyPath, "*.md", SearchOption.AllDirectories);
        if (mdFiles.Length == 0)
        {
            _logger.LogWarning("No markdown files found for property: {PropertyName}", propertyName);
            return string.Empty;
        }

        var keywords = ExtractKeywords(guestMessage);
        var scoredSections = new List<(int Score, string FileName, string Section)>();

        foreach (var file in mdFiles)
        {
            var content = await File.ReadAllTextAsync(file);
            var fileName = Path.GetFileNameWithoutExtension(file);
            var sections = SplitIntoSections(content);

            foreach (var section in sections)
            {
                var score = ScoreSection(section, keywords);
                if (score >= MinimumScore)
                {
                    scoredSections.Add((score, fileName, section));
                }
            }
        }

        if (scoredSections.Count == 0)
        {
            return string.Empty;
        }

        var topSections = scoredSections
            .OrderByDescending(s => s.Score)
            .Take(TopSectionsToReturn)
            .ToList();

        var contextParts = topSections.Select(s => $"[{s.FileName}]\n{s.Section.Trim()}");
        return string.Join("\n\n---\n\n", contextParts);
    }

    private string GetPropertyPath(string propertyName) =>
        Path.Combine(_memoryBasePath, propertyName);

    private static List<string> SplitIntoSections(string markdown)
    {
        // Split on markdown headings (## or ###) to get logical sections
        var lines = markdown.Split('\n');
        var sections = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if ((line.StartsWith("## ") || line.StartsWith("### ")) && current.Length > 0)
            {
                sections.Add(current.ToString());
                current.Clear();
            }
            current.AppendLine(line);
        }

        if (current.Length > 0)
            sections.Add(current.ToString());

        return sections;
    }

    private static HashSet<string> ExtractKeywords(string message)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "is", "are", "do", "you", "have", "has", "we",
            "i", "me", "my", "and", "or", "for", "in", "on", "at", "to",
            "it", "its", "be", "was", "will", "can", "any", "there", "about"
        };

        return message
            .ToLowerInvariant()
            .Split([' ', ',', '.', '?', '!', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int ScoreSection(string section, HashSet<string> keywords)
    {
        var sectionLower = section.ToLowerInvariant();
        return keywords.Count(keyword => sectionLower.Contains(keyword));
    }
}
