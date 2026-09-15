using System.Linq;

EnsureDataLoaded();

// Polish diacritics we need: ą Ą ć Ć ę Ę ł Ł ń Ń ó Ó ś Ś ź Ź ż Ż
string polishChars = "ąĄćĆęĘłŁńŃóÓśŚźŹżŻ";

foreach (var font in Data.Fonts)
{
    var codes = new HashSet<int>(font.Glyphs.Select(g => (int)g.Character));
    var missing = polishChars.Where(c => !codes.Contains((int)c)).ToList();
    System.Console.WriteLine($"Font '{font.Name.Content}': {font.Glyphs.Count} glyphs, range {font.RangeStart}-{font.RangeEnd}, missing {missing.Count}/{polishChars.Length} Polish chars: {string.Join(" ", missing)}");
}
