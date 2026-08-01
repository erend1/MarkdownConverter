using MarkdownConverter.Infrastructure.Bibliography;

namespace MarkdownConverter.Infrastructure.Tests;

public class BibTexParserTests
{
    [Fact]
    public void Parse_SingleArticle_ReturnsEntry()
    {
        var bib = """
            @article{smith2023,
                author = {Smith, John},
                title = {A Great Paper},
                year = {2023},
                journal = {Nature}
            }
            """;

        var result = BibTexParser.Parse(bib);

        Assert.Single(result);
        Assert.True(result.ContainsKey("smith2023"));
        var entry = result["smith2023"];
        Assert.Equal("article", entry.EntryType);
        Assert.Equal("Smith, John", entry.Author);
        Assert.Equal("A Great Paper", entry.Title);
        Assert.Equal("2023", entry.Year);
        Assert.Equal("Nature", entry.Journal);
    }

    [Fact]
    public void Parse_MultipleEntries_ReturnsAll()
    {
        var bib = """
            @article{key1,
                author = {Author One},
                title = {Title One},
                year = {2020}
            }
            @book{key2,
                author = {Author Two},
                title = {Title Two},
                year = {2021},
                publisher = {Springer}
            }
            """;

        var result = BibTexParser.Parse(bib);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("key1"));
        Assert.True(result.ContainsKey("key2"));
        Assert.Equal("article", result["key1"].EntryType);
        Assert.Equal("book", result["key2"].EntryType);
        Assert.Equal("Springer", result["key2"].Publisher);
    }

    [Fact]
    public void Parse_QuotedValues_Handled()
    {
        var bib = """
            @article{test1,
                author = "Smith, John",
                title = "Quoted Title",
                year = "2023"
            }
            """;

        var result = BibTexParser.Parse(bib);

        Assert.Single(result);
        Assert.Equal("Smith, John", result["test1"].Author);
        Assert.Equal("Quoted Title", result["test1"].Title);
    }

    [Fact]
    public void Parse_NestedBraces_Handled()
    {
        var bib = """
            @article{test2,
                title = {The {LaTeX} Project},
                author = {Smith, John},
                year = {2023}
            }
            """;

        var result = BibTexParser.Parse(bib);

        Assert.Equal("The {LaTeX} Project", result["test2"].Title);
    }

    [Fact]
    public void Parse_AllFields_Populated()
    {
        var bib = """
            @article{full,
                author = {Doe, Jane},
                title = {Full Entry},
                year = {2024},
                journal = {Science},
                volume = {42},
                number = {3},
                pages = {100--120},
                doi = {10.1234/test},
                url = {https://example.com},
                publisher = {Elsevier}
            }
            """;

        var result = BibTexParser.Parse(bib);

        var entry = result["full"];
        Assert.Equal("Doe, Jane", entry.Author);
        Assert.Equal("Full Entry", entry.Title);
        Assert.Equal("2024", entry.Year);
        Assert.Equal("Science", entry.Journal);
        Assert.Equal("42", entry.Volume);
        Assert.Equal("3", entry.Number);
        Assert.Equal("100--120", entry.Pages);
        Assert.Equal("10.1234/test", entry.Doi);
        Assert.Equal("https://example.com", entry.Url);
        Assert.Equal("Elsevier", entry.Publisher);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(BibTexParser.Parse(""));
        Assert.Empty(BibTexParser.Parse("   "));
    }

    [Fact]
    public void Parse_CommentAndPreamble_Skipped()
    {
        var bib = """
            @comment{this is a comment}
            @preamble{some preamble}
            @article{real,
                author = {Test},
                title = {Real},
                year = {2023}
            }
            """;

        var result = BibTexParser.Parse(bib);

        Assert.Single(result);
        Assert.True(result.ContainsKey("real"));
    }

    [Fact]
    public void Parse_UnquotedNumericYear()
    {
        var bib = """
            @article{test,
                author = {Smith},
                title = {Test},
                year = 2023
            }
            """;

        var result = BibTexParser.Parse(bib);

        Assert.Equal("2023", result["test"].Year);
    }

    [Fact]
    public void Parse_InProceedings_TypePreserved()
    {
        var bib = """
            @inproceedings{conf1,
                author = {Author},
                title = {Conference Paper},
                booktitle = {ICSE 2023},
                year = {2023}
            }
            """;

        var result = BibTexParser.Parse(bib);

        Assert.Equal("inproceedings", result["conf1"].EntryType);
        Assert.Equal("ICSE 2023", result["conf1"].BookTitle);
    }
}
