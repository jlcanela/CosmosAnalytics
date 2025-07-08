public class LowercaseAsciiFoldingAnalyzer : Analyzer
{
    public static string[] ProcessText(string input)
    {
        var analyzer = new LowercaseAsciiFoldingAnalyzer(Lucene.Net.Util.LuceneVersion.LUCENE_48);
        var tokens = new List<string>();

        using (var tokenStream = analyzer.GetTokenStream("field", new StringReader(input)))
        {
            tokenStream.Reset();
            var termAttr = tokenStream.GetAttribute<ICharTermAttribute>();
            while (tokenStream.IncrementToken())
            {
                tokens.Add(termAttr.ToString());
            }
            tokenStream.End();
        }

        return tokens.ToArray();
    }
    private readonly LuceneVersion _version;

    public LowercaseAsciiFoldingAnalyzer(LuceneVersion version)
    {
        _version = version;
    }

    protected override TokenStreamComponents CreateComponents(string fieldName, System.IO.TextReader reader)
    {
        var tokenizer = new StandardTokenizer(_version, reader);
        TokenStream tokenStream = new LowerCaseFilter(_version, tokenizer);
        tokenStream = new ASCIIFoldingFilter(tokenStream);
        return new TokenStreamComponents(tokenizer, tokenStream);
    }
}

