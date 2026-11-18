using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;

namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

/// <summary>
/// GitHub's pagination: the address of the next page is in the response's <c>Link</c> header, with the relation
/// type <c>next</c>; a response without it is the last page.
/// </summary>
/// <remarks>
/// The header is read as RFC 8288 (Web Linking) defines it, not only in the form GitHub sends today:
/// <c>&lt;address&gt;; rel="next", &lt;address&gt;; rel="last"</c>. The relation type may be quoted or not, in any
/// case, and among several types; a comma or semicolon inside the address or a quoted value does not split it.
/// </remarks>
internal static class GitHubPagination
{
    private const string LinkHeaderName = "Link";
    private const string RelationParameterName = "rel";

    // A link relation type registered by RFC 8288.
    private const string NextRelationType = "next";

    // The header lists the links separated by commas; a link is its address and its parameters, separated by
    // semicolons; a parameter is name=value:
    // <https://api.github.com/...?page=2>; rel="next", <https://api.github.com/...?page=5>; rel="last"
    private const char LinkSeparator = ',';
    private const char ParameterSeparator = ';';
    private const char NameValueSeparator = '=';

    // The address of a link is between angle brackets, and a parameter value may be between quotes.
    private const char AddressStart = '<';
    private const char AddressEnd = '>';
    private const char Quote = '"';

    // The relation types in one rel value are separated by spaces: rel="prefetch next".
    private const char RelationTypeSeparator = ' ';

    /// <summary>The address of the next page; <see langword="null"/> on the last page.</summary>
    public static Uri? NextPageUri(HttpResponseMessage response, Uri apiAddress)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(apiAddress);

        if (!response.Headers.TryGetValues(LinkHeaderName, out var linkHeaders))
        {
            return null;
        }

        var links = linkHeaders.SelectMany(header => SplitOutsideAddressAndQuotes(header, LinkSeparator));
        foreach (var link in links)
        {
            // One link: <address>; name=value; name="value"
            var linkParts = SplitOutsideAddressAndQuotes(link, ParameterSeparator);
            var target = linkParts.First().Trim();
            var parameters = linkParts.Skip(1);

            var isAddress = target.StartsWith(AddressStart) && target.EndsWith(AddressEnd);
            if (!isAddress || !parameters.Any(IsNextRelation))
            {
                continue;
            }

            var address = target.TrimStart(AddressStart).TrimEnd(AddressEnd);
            var nextPage = new Uri(address, UriKind.Absolute);

            // The client sends the token with every request, so only an address of the same API is followed.
            if (!apiAddress.IsBaseOf(nextPage))
            {
                throw new InvalidOperationException(GitHubErrors.NextPageOutsideApi(nextPage));
            }

            return nextPage;
        }

        return null;
    }

    /// <summary>Whether a link parameter is a <c>rel</c> with the <c>next</c> relation type among its types.</summary>
    private static bool IsNextRelation(string parameter)
    {
        // A parameter: name=value, for example rel="next". Split at the first '=' only: a quoted value may
        // contain '=' too.
        var nameAndValue = parameter.Split(NameValueSeparator, 2);
        if (nameAndValue.Length != 2)
        {
            return false;
        }

        var name = nameAndValue.First().Trim();
        if (!string.Equals(name, RelationParameterName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // rel="next" and rel=next mean the same, so the quotes are taken off.
        var value = nameAndValue.Last().Trim().Trim(Quote);
        var relationTypes = value.Split(RelationTypeSeparator, StringSplitOptions.RemoveEmptyEntries);
        return relationTypes.Any(type => string.Equals(type, NextRelationType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Splits the text at the separator, except inside an address (&lt;...&gt;) or a quoted value: there a comma or
    /// semicolon is part of the text, for example in <c>?q=a,b</c>.
    /// </summary>
    private static List<string> SplitOutsideAddressAndQuotes(string text, char separator)
    {
        var pieces = new List<string>();
        var pieceStart = 0;
        var isInsideAddress = false;
        var isInsideQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            // A quote only counts outside an address, and angle brackets only outside quotes: inside one, the
            // other is an ordinary character.
            if (character == Quote && !isInsideAddress)
            {
                isInsideQuotes = !isInsideQuotes;
            }
            else if (character == AddressStart && !isInsideQuotes)
            {
                isInsideAddress = true;
            }
            else if (character == AddressEnd && !isInsideQuotes)
            {
                isInsideAddress = false;
            }
            else if (character == separator && !isInsideAddress && !isInsideQuotes)
            {
                pieces.Add(text.Substring(pieceStart, index - pieceStart));
                pieceStart = index + 1;
            }
        }

        pieces.Add(text.Substring(pieceStart));
        return pieces;
    }
}
