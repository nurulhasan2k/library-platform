using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.Options;

namespace Library.Core.Import;

public sealed class CatalogueCsvValidator(IClock clock, IOptions<ImportOptions> options)
{
    public static IReadOnlyList<string> Columns { get; } = Array.AsReadOnly(new[]
    {
        "LibraryCode", "ItemType", "Name", "Description", "Category", "Branch", "Status",
        "Author", "Genre", "Artist", "Year", "ToyType", "RecommendedAge"
    });
    private static readonly string[] RequiredColumns = ["LibraryCode", "ItemType", "Name", "Category"];
    private static readonly Dictionary<string, int> LengthLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LibraryCode"] = 30, ["ItemType"] = 20, ["Name"] = 200, ["Description"] = 4000,
        ["Category"] = 100, ["Branch"] = 30, ["Status"] = 20, ["Author"] = 200,
        ["Genre"] = 100, ["Artist"] = 200, ["Year"] = 10, ["ToyType"] = 100, ["RecommendedAge"] = 10
    };

    public ImportValidationResult Validate(Stream file, string fileName, ImportLookups lookups, int? targetBranchId = null)
    {
        var settings = options.Value;
        if (!string.Equals(Path.GetExtension(fileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return Failure("The file must have a .csv extension.");
        if (settings.MaxFileSizeMb <= 0 || settings.MaxRows <= 0)
            return Failure("Import size and row limits must be greater than zero.");
        if (targetBranchId is not null && !lookups.Branches.Any(branch => branch.Id == targetBranchId))
            return Failure("The target branch does not exist.");

        var maximumBytes = settings.MaxFileSizeMb * 1024L * 1024L;
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = file.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
                return Failure($"The file exceeds {settings.MaxFileSizeMb} MB.");
            buffer.Write(chunk, 0, read);
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(buffer.ToArray());
        }
        catch (DecoderFallbackException)
        {
            return Failure("The file must contain valid UTF-8 text.");
        }
        if (text.StartsWith('\uFEFF'))
            text = text[1..];

        var malformed = false;
        using var reader = new StringReader(text);
        using var parser = new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            IgnoreBlankLines = false,
            BadDataFound = _ => malformed = true,
            ExceptionMessagesContainRawData = false
        });
        var valid = new List<ValidItemRow>();
        var rejected = new List<ImportRowResult>();
        try
        {
            if (!parser.Read())
                return Failure("The CSV header is missing.");
            var header = parser.Record!.Select(value => value.Trim()).ToArray();
            if (malformed)
                return Failure("The CSV header contains malformed quoting.");
            var missing = RequiredColumns.Where(column => !header.Contains(column, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (missing.Length > 0)
                return Failure("Missing required columns: " + string.Join(", ", missing) + ".");
            if (header.Distinct(StringComparer.OrdinalIgnoreCase).Count() != header.Length)
                return Failure("The CSV header contains duplicate columns.");

            var positions = header.Select((name, index) => (name, index))
                .ToDictionary(pair => pair.name, pair => pair.index, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existing = new HashSet<string>(lookups.ExistingCodes.Select(code => code.Trim()), StringComparer.OrdinalIgnoreCase);
            var currentYear = clock.Today.Year;
            while (true)
            {
                var rowNumber = parser.RawRow + 1;
                malformed = false;
                if (!parser.Read())
                    break;
                var record = parser.Record!;
                if (string.IsNullOrWhiteSpace(parser.RawRecord))
                    continue;
                if (valid.Count + rejected.Count >= settings.MaxRows)
                    return new ImportValidationResult([], rejected, [$"The file exceeds {settings.MaxRows} data rows."]);

                var fields = Columns.ToDictionary(column => column,
                    column => positions.TryGetValue(column, out var index) && index < record.Length ? record[index] : "",
                    StringComparer.OrdinalIgnoreCase);
                var errors = new List<string>();
                if (malformed)
                    errors.Add("The row contains malformed CSV quoting.");
                if (record.Length != header.Length)
                    errors.Add("The row has a different number of fields from the header.");
                if (record.Any(value => value.Length > 4000))
                    errors.Add("A field must be at most 4000 characters, including unrecognised columns.");
                foreach (var (column, value) in fields)
                {
                    if (value.Length > LengthLimits[column])
                        errors.Add($"{column} must be at most {LengthLimits[column]} characters.");
                    if (value.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
                        errors.Add($"{column} contains an invalid control character.");
                }
                string Value(string column) => fields[column].Trim();
                foreach (var column in RequiredColumns)
                    if (Value(column).Length == 0)
                        errors.Add($"{column} is required.");

                var code = Value("LibraryCode");
                if (code.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
                    errors.Add("LibraryCode may contain only letters, digits, - and _.");
                if (code.Length > 0 && !seen.Add(code))
                    errors.Add("LibraryCode repeats inside this file.");
                if (existing.Contains(code))
                    errors.Add("LibraryCode already exists in the catalogue.");

                ItemType? type = Value("ItemType").ToLowerInvariant() switch
                {
                    "book" => ItemType.Book, "music" => ItemType.Music, "toy" => ItemType.Toy, _ => null
                };
                if (type is null)
                    errors.Add("ItemType must be book, music or toy.");
                ItemStatus? status = Value("Status").ToLowerInvariant() switch
                {
                    "" or "available" => ItemStatus.Available, "damaged" => ItemStatus.Damaged, _ => null
                };
                if (status is null)
                    errors.Add("Status must be Available or Damaged.");

                var branch = Value("Branch").Length > 0
                    ? lookups.Branches.FirstOrDefault(candidate => candidate.Code.Equals(Value("Branch"), StringComparison.OrdinalIgnoreCase))
                    : lookups.Branches.FirstOrDefault(candidate => candidate.Id == targetBranchId);
                if (branch is null)
                    errors.Add("Branch must name an existing branch, or a valid target branch must be supplied.");
                var categories = lookups.Categories.Where(candidate => candidate.Name.Equals(Value("Category"), StringComparison.OrdinalIgnoreCase)).ToArray();
                var category = categories.FirstOrDefault(candidate => candidate.ItemType == type);
                if (categories.Length == 0)
                    errors.Add("Category does not exist.");
                else if (type is not null && category is null)
                    errors.Add("Category does not belong to this ItemType.");

                string[] typeFields = type switch
                {
                    ItemType.Book => ["Author", "Genre"], ItemType.Music => ["Artist", "Year"],
                    ItemType.Toy => ["ToyType", "RecommendedAge"], _ => []
                };
                foreach (var column in typeFields)
                    if (Value(column).Length == 0)
                        errors.Add($"{column} is required for this ItemType.");
                int? year = null;
                int? age = null;
                if (type == ItemType.Music)
                {
                    if (int.TryParse(Value("Year"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1000 && parsed <= currentYear)
                        year = parsed;
                    else
                        errors.Add($"Year must be an integer from 1000 to {currentYear}.");
                }
                if (type == ItemType.Toy)
                {
                    if (int.TryParse(Value("RecommendedAge"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 18)
                        age = parsed;
                    else
                        errors.Add("RecommendedAge must be an integer from 0 to 18.");
                }
                if (errors.Count > 0)
                    rejected.Add(new ImportRowResult(rowNumber, fields, errors));
                else
                    valid.Add(new ValidItemRow(rowNumber, code, type!.Value, Value("Name"), Value("Description"),
                        category!.Id, branch!.Id, status!.Value, Value("Author"), Value("Genre"),
                        Value("Artist"), year, Value("ToyType"), age));
            }
        }
        catch (CsvHelperException)
        {
            return new ImportValidationResult([], rejected, ["The CSV file could not be parsed. Check its quoting and field sizes."]);
        }
        return new ImportValidationResult(valid, rejected, []);
    }

    private static ImportValidationResult Failure(string message) => new([], [], [message]);
}
