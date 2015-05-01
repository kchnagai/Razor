// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNet.Razor.Generator;
using Microsoft.AspNet.Razor.Parser;
using Microsoft.AspNet.Razor.Parser.SyntaxTree;
using Microsoft.AspNet.Razor.Test.Framework;
using Microsoft.AspNet.Razor.Text;
using Microsoft.AspNet.Razor.Tokenizer;
using Xunit;

namespace Microsoft.AspNet.Razor.Test.Parser.Html
{
    public class HtmlDocumentTest : CsHtmlMarkupParserTestBase
    {
        private static readonly TestFile Nested1000 = TestFile.Create("TestFiles/nested-1000.html");

        [Fact]
        public void ParseDocumentMethodThrowsArgNullExceptionOnNullContext()
        {
            // Arrange
            var parser = new HtmlMarkupParser();

            // Act and Assert
            var exception = Assert.Throws<InvalidOperationException>(() => parser.ParseDocument());
            Assert.Equal(RazorResources.Parser_Context_Not_Set, exception.Message);
        }

        [Fact]
        public void ParseSectionMethodThrowsArgNullExceptionOnNullContext()
        {
            // Arrange
            var parser = new HtmlMarkupParser();

            // Act and Assert
            var exception = Assert.Throws<InvalidOperationException>(() => parser.ParseSection(null, true));
            Assert.Equal(RazorResources.Parser_Context_Not_Set, exception.Message);
        }

        [Fact]
        public void ParseDocumentOutputsEmptyBlockWithEmptyMarkupSpanIfContentIsEmptyString()
        {
            ParseDocumentTest(string.Empty, new MarkupBlock(Factory.EmptyHtml()));
        }

        [Fact]
        public void ParseDocumentOutputsWhitespaceOnlyContentAsSingleWhitespaceMarkupSpan()
        {
            SingleSpanDocumentTest("          ", BlockType.Markup, SpanKind.Markup);
        }

        [Fact]
        public void ParseDocumentAcceptsSwapTokenAtEndOfFileAndOutputsZeroLengthCodeSpan()
        {
            ParseDocumentTest("@",
                new MarkupBlock(
                    Factory.EmptyHtml(),
                    new ExpressionBlock(
                        Factory.CodeTransition(),
                        Factory.EmptyCSharp()
                               .AsImplicitExpression(CSharpCodeParser.DefaultKeywords)
                               .Accepts(AcceptedCharacters.NonWhiteSpace)),
                    Factory.EmptyHtml()),
                new RazorError(RazorResources.ParseError_Unexpected_EndOfFile_At_Start_Of_CodeBlock, 1, 0, 1));
        }

        [Fact]
        public void ParseDocumentCorrectlyHandlesOddlySpacedHTMLElements()
        {
            ParseDocumentTest("<div ><p class = 'bar'> Foo </p></div >",
                new MarkupBlock(
                    BlockFactory.MarkupTagBlock("<div >"),
                    BlockFactory.MarkupTagBlock("<p class = 'bar'>"),
                    Factory.Markup(" Foo "),
                    BlockFactory.MarkupTagBlock("</p>"),
                    BlockFactory.MarkupTagBlock("</div >")));
        }

        [Fact]
        public void ParseDocumentCorrectlyHandlesSingleLineOfMarkupWithEmbeddedStatement()
        {
            ParseDocumentTest("<div>Foo @if(true) {} Bar</div>",
                new MarkupBlock(
                    BlockFactory.MarkupTagBlock("<div>"),
                    Factory.Markup("Foo "),
                    new StatementBlock(
                        Factory.CodeTransition(),
                        Factory.Code("if(true) {}").AsStatement()),
                    Factory.Markup(" Bar"),
                    BlockFactory.MarkupTagBlock("</div>")));
        }

        [Fact]
        public void ParseDocumentWithinSectionDoesNotCreateDocumentLevelSpan()
        {
            ParseDocumentTest("@section Foo {" + Environment.NewLine
                            + "    <html></html>" + Environment.NewLine
                            + "}",
                new MarkupBlock(
                    Factory.EmptyHtml(),
                    new SectionBlock(new SectionCodeGenerator("Foo"),
                        Factory.CodeTransition(),
                        Factory.MetaCode("section Foo {")
                               .AutoCompleteWith(null, atEndOfSpan: true),
                        new MarkupBlock(
                            Factory.Markup(Environment.NewLine + "    "),
                            BlockFactory.MarkupTagBlock("<html>"),
                            BlockFactory.MarkupTagBlock("</html>"),
                            Factory.Markup(Environment.NewLine)),
                        Factory.MetaCode("}").Accepts(AcceptedCharacters.None)),
                    Factory.EmptyHtml()));
        }

        [Fact]
        public void ParseDocumentParsesWholeContentAsOneSpanIfNoSwapCharacterEncountered()
        {
            SingleSpanDocumentTest("foo baz", BlockType.Markup, SpanKind.Markup);
        }

        [Fact]
        public void ParseDocumentHandsParsingOverToCodeParserWhenAtSignEncounteredAndEmitsOutput()
        {
            ParseDocumentTest("foo @bar baz",
                new MarkupBlock(
                    Factory.Markup("foo "),
                    new ExpressionBlock(
                        Factory.CodeTransition(),
                        Factory.Code("bar")
                               .AsImplicitExpression(CSharpCodeParser.DefaultKeywords)
                               .Accepts(AcceptedCharacters.NonWhiteSpace)),
                    Factory.Markup(" baz")));
        }

        [Fact]
        public void ParseDocumentEmitsAtSignAsMarkupIfAtEndOfFile()
        {
            ParseDocumentTest("foo @",
                new MarkupBlock(
                    Factory.Markup("foo "),
                    new ExpressionBlock(
                        Factory.CodeTransition(),
                        Factory.EmptyCSharp()
                               .AsImplicitExpression(CSharpCodeParser.DefaultKeywords)
                               .Accepts(AcceptedCharacters.NonWhiteSpace)),
                    Factory.EmptyHtml()),
                new RazorError(RazorResources.ParseError_Unexpected_EndOfFile_At_Start_Of_CodeBlock, 5, 0, 5));
        }

        [Fact]
        public void ParseDocumentEmitsCodeBlockIfFirstCharacterIsSwapCharacter()
        {
            ParseDocumentTest("@bar",
                new MarkupBlock(
                    Factory.EmptyHtml(),
                    new ExpressionBlock(
                        Factory.CodeTransition(),
                        Factory.Code("bar")
                               .AsImplicitExpression(CSharpCodeParser.DefaultKeywords)
                               .Accepts(AcceptedCharacters.NonWhiteSpace)),
                    Factory.EmptyHtml()));
        }

        [Fact]
        public void ParseDocumentDoesNotSwitchToCodeOnEmailAddressInText()
        {
            SingleSpanDocumentTest("anurse@microsoft.com", BlockType.Markup, SpanKind.Markup);
        }

        [Fact]
        public void ParseDocumentDoesNotSwitchToCodeOnEmailAddressInAttribute()
        {
            ParseDocumentTest("<a href=\"mailto:anurse@microsoft.com\">Email me</a>",
                new MarkupBlock(
                    new MarkupTagBlock(
                        Factory.Markup("<a"),
                        new MarkupBlock(new AttributeBlockCodeGenerator("href", new LocationTagged<string>(" href=\"", 2, 0, 2), new LocationTagged<string>("\"", 36, 0, 36)),
                            Factory.Markup(" href=\"").With(SpanCodeGenerator.Null),
                            Factory.Markup("mailto:anurse@microsoft.com")
                                   .With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 9, 0, 9), new LocationTagged<string>("mailto:anurse@microsoft.com", 9, 0, 9))),
                            Factory.Markup("\"").With(SpanCodeGenerator.Null)),
                        Factory.Markup(">")),
                    Factory.Markup("Email me"),
                    BlockFactory.MarkupTagBlock("</a>")));
        }

        [Fact]
        public void ParseDocumentDoesNotReturnErrorOnMismatchedTags()
        {
            ParseDocumentTest("Foo <div><p></p></p> Baz",
                new MarkupBlock(
                    Factory.Markup("Foo "),
                    BlockFactory.MarkupTagBlock("<div>"),
                    BlockFactory.MarkupTagBlock("<p>"),
                    BlockFactory.MarkupTagBlock("</p>"),
                    BlockFactory.MarkupTagBlock("</p>"),
                    Factory.Markup(" Baz")));
        }

        [Fact]
        public void ParseDocumentReturnsOneMarkupSegmentIfNoCodeBlocksEncountered()
        {
            SingleSpanDocumentTest("Foo Baz<!--Foo-->Bar<!--F> Qux", BlockType.Markup, SpanKind.Markup);
        }

        [Fact]
        public void ParseDocumentRendersTextPseudoTagAsMarkup()
        {
            ParseDocumentTest("Foo <text>Foo</text>",
                new MarkupBlock(
                    Factory.Markup("Foo "),
                    BlockFactory.MarkupTagBlock("<text>"),
                    Factory.Markup("Foo"),
                    BlockFactory.MarkupTagBlock("</text>")));
        }

        [Fact]
        public void ParseDocumentAcceptsEndTagWithNoMatchingStartTag()
        {
            ParseDocumentTest("Foo </div> Bar",
                new MarkupBlock(
                    Factory.Markup("Foo "),
                    BlockFactory.MarkupTagBlock("</div>"),
                    Factory.Markup(" Bar")));
        }

        [Fact]
        public void ParseDocumentNoLongerSupportsDollarOpenBraceCombination()
        {
            ParseDocumentTest("<foo>${bar}</foo>",
                new MarkupBlock(
                    BlockFactory.MarkupTagBlock("<foo>"),
                    Factory.Markup("${bar}"),
                    BlockFactory.MarkupTagBlock("</foo>")));
        }

        [Fact]
        public void ParseDocumentIgnoresTagsInContentsOfScriptTag()
        {
            ParseDocumentTest(@"<script>foo<bar baz='@boz'></script>",
                new MarkupBlock(
                    BlockFactory.MarkupTagBlock("<script>"),
                    Factory.Markup("foo<bar baz='"),
                    new ExpressionBlock(
                        Factory.CodeTransition(),
                        Factory.Code("boz")
                               .AsImplicitExpression(CSharpCodeParser.DefaultKeywords, acceptTrailingDot: false)
                               .Accepts(AcceptedCharacters.NonWhiteSpace)),
                    Factory.Markup("'>"),
                    BlockFactory.MarkupTagBlock("</script>")));
        }

        [Fact]
        public void ParseSectionIgnoresTagsInContentsOfScriptTag()
        {
            ParseDocumentTest(@"@section Foo { <script>foo<bar baz='@boz'></script> }",
                new MarkupBlock(
                    Factory.EmptyHtml(),
                    new SectionBlock(new SectionCodeGenerator("Foo"),
                        Factory.CodeTransition(),
                        Factory.MetaCode("section Foo {")
                            .AutoCompleteWith(autoCompleteString: null, atEndOfSpan: true),
                        new MarkupBlock(
                            Factory.Markup(" "),
                            BlockFactory.MarkupTagBlock("<script>"),
                            Factory.Markup("foo<bar baz='"),
                            new ExpressionBlock(
                                Factory.CodeTransition(),
                                Factory.Code("boz")
                                       .AsImplicitExpression(CSharpCodeParser.DefaultKeywords, acceptTrailingDot: false)
                                       .Accepts(AcceptedCharacters.NonWhiteSpace)),
                            Factory.Markup("'>"),
                            BlockFactory.MarkupTagBlock("</script>"),
                            Factory.Markup(" ")),
                        Factory.MetaCode("}").Accepts(AcceptedCharacters.None)),
                    Factory.EmptyHtml()));
        }

        [Fact]
        public void ParseBlockCanParse1000NestedElements()
        {
            var content = Nested1000.ReadAllText();
            ParseDocument(content);
        }

        public static TheoryData ParseBlockData
        {
            get
            {
                var factory = CreateDefaultSpanFactory();

                return new TheoryData<string, Block>
                {
                    {
                        "<span foo='@@' />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>("'", 13, 0, 13)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup("'").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                    {
                        "<span foo='abc@@' />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>("'", 16, 0, 16)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    factory.Markup("abc").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 11, 0, 11), new LocationTagged<string>("abc", 11, 0, 11))),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup("'").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                    {
                        "<span foo='@@def' />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>("'", 16, 0, 16)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup("def").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 13, 0, 13), new LocationTagged<string>("def", 13, 0, 13))),
                                    factory.Markup("'").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                    {
                        "<span foo='abc @@ def' />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>("'", 21, 0, 21)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    factory.Markup("abc").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 11, 0, 11), new LocationTagged<string>("abc", 11, 0, 11))),
                                    factory.Markup(" "),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup(" def").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(" ", 17, 0, 17), new LocationTagged<string>("def", 18, 0, 18))),
                                    factory.Markup("'").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                    {
                        "<span foo='@@@DateTime.Now' />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>("'", 26, 0, 26)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    new MarkupBlock(
                                        new DynamicAttributeBlockCodeGenerator(new LocationTagged<string>(string.Empty, 13, 0, 13), 13, 0, 13),
                                        new ExpressionBlock(
                                            factory.CodeTransition(),
                                            factory.Code("DateTime.Now")
                                                .AsImplicitExpression(CSharpCodeParser.DefaultKeywords)
                                                .Accepts(AcceptedCharacters.NonWhiteSpace))),
                                    factory.Markup("'").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                    {
                        "<span foo='@DateTime.Now @@' />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>("'", 27, 0, 27)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    new MarkupBlock(
                                        new DynamicAttributeBlockCodeGenerator(new LocationTagged<string>(string.Empty, 11, 0, 11), 11, 0, 11),
                                        new ExpressionBlock(
                                            factory.CodeTransition(),
                                            factory.Code("DateTime.Now")
                                                .AsImplicitExpression(CSharpCodeParser.DefaultKeywords)
                                                .Accepts(AcceptedCharacters.NonWhiteSpace))),
                                    factory.Markup(" "),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup("'").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                    {
                        "<span foo='abc@def.com @@' />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>("'", 25, 0, 25)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    factory.Markup("abc@def.com").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 11, 0, 11), new LocationTagged<string>("abc@def.com", 11, 0, 11))),
                                    factory.Markup(" "),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup("'").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                    {
                        "<span foo='abc@@def.com @@' />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>("'", 26, 0, 26)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    factory.Markup("abc").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 11, 0, 11), new LocationTagged<string>("abc", 11, 0, 11))),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup("def.com").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 16, 0, 16), new LocationTagged<string>("def.com", 16, 0, 16))),
                                    factory.Markup(" "),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup("'").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                    {
                        "<span foo='@@",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo='", 5, 0, 5), new LocationTagged<string>(string.Empty, 13, 0, 13)),
                                    factory.Markup(" foo='").With(SpanCodeGenerator.Null),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"))))
                    },
                    {
                        @"<span foo=""/^[a-z0-9!#$%&'*+\/=?^_`{|}~.-]+@@[a-z0-9]([a-z0-9-]*[a-z0-9])?\.([a-z0-9]([a-z0-9-]*[a-z0-9])?)*$/i"" />",
                        new MarkupBlock(
                            new MarkupTagBlock(
                                factory.Markup("<span"),
                                new MarkupBlock(
                                    new AttributeBlockCodeGenerator("foo", new LocationTagged<string>(" foo=\"", 5, 0, 5), new LocationTagged<string>("\"", 111, 0, 111)),
                                    factory.Markup(" foo=\"").With(SpanCodeGenerator.Null),
                                    factory.Markup(@"/^[a-z0-9!#$%&'*+\/=?^_`{|}~.-]+").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 11, 0, 11), new LocationTagged<string>(@"/^[a-z0-9!#$%&'*+\/=?^_`{|}~.-]+", 11, 0, 11))),
                                    factory.Markup("@").With(SpanCodeGenerator.Null),
                                    factory.Markup("@"),
                                    factory.Markup(@"[a-z0-9]([a-z0-9-]*[a-z0-9])?\.([a-z0-9]([a-z0-9-]*[a-z0-9])?)*$/i").With(new LiteralAttributeCodeGenerator(new LocationTagged<string>(string.Empty, 45, 0, 45), new LocationTagged<string>(@"[a-z0-9]([a-z0-9-]*[a-z0-9])?\.([a-z0-9]([a-z0-9-]*[a-z0-9])?)*$/i", 45, 0, 45))),
                                    factory.Markup("\"").With(SpanCodeGenerator.Null)),
                                factory.Markup(" />")))
                    },
                };
            }
        }

        [Theory]
        [MemberData(nameof(ParseBlockData))]
        public void ParseBlock_WithDoubleTransition_DoesNotThrow(string input, Block expected)
        {
            ParseDocumentTest(input, expected);
        }

        private static SpanFactory CreateDefaultSpanFactory()
        {
            return new SpanFactory
            {
                MarkupTokenizerFactory = doc => new HtmlTokenizer(doc),
                CodeTokenizerFactory = doc => new CSharpTokenizer(doc)
            };
        }
    }
}
