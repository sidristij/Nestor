using System;
using System.Linq;
using Nestor;
using NUnit.Framework;

namespace NestorTests
{
    [TestFixture]
    public class Tests
    {
        private NestorMorph _nMorph;
        
        [SetUp]
        public void SetUp()
        {
            _nMorph = new NestorMorph();
        }
        
        [Test]
        public void TestTokenize()
        {
            var tokens = _nMorph.Tokenize("Пришёл, увидел, победил. 123-45, 67//# Как-то раз.");
            Assert.AreEqual(5, tokens.Length);
            Assert.AreEqual("пришёл", tokens[0]);
            Assert.AreEqual("как-то", tokens[3]);

            var tokensNum = _nMorph.Tokenize("В 140 солнц закат пылал.", MorphOption.KeepNumbers);
            Assert.AreEqual(5, tokensNum.Length);
            Assert.AreEqual("140", tokensNum[1]);

            var tokensPrepositions = _nMorph.Tokenize("Под о ш в а", MorphOption.RemovePrepositions);
            Assert.AreEqual(1, tokensPrepositions.Length);
            Assert.AreEqual("ш", tokensPrepositions[0]);

            var tokensExistent = _nMorph.Tokenize(
                "Съешь ещё этих бурдылек и выпей куздру",
                MorphOption.RemoveNonExistent
            );
            Assert.AreEqual(5, tokensExistent.Length);
            Assert.False(tokensExistent.Contains("бурдылек"));
        }

        [Test]
        public void TestLemmatize()
        {
            var lemmas = _nMorph.Lemmatize("Кошки стали бурлеть в округе");
            Assert.AreEqual(5, lemmas.Length);
            Assert.True(lemmas.Contains("кошка"));

            var lemmasFull = _nMorph.Lemmatize(
                "Кошки стали бурлеть в округе",
                MorphOption.InsertAllLemmas | MorphOption.Distinct
            );
            Assert.AreEqual(7, lemmasFull.Length);
            Assert.True(lemmasFull.Contains("сталь"));
            Assert.True(lemmasFull.Contains("стать"));
            Assert.True(lemmasFull.Contains("округ"));
            Assert.True(lemmasFull.Contains("округа"));
        }

        [Test]
        public void TestWordInfo()
        {
            const string word = "стали";
            var info = _nMorph.WordInfo(word);
            Assert.AreEqual(2, info.Length);

            // first
            var first = info.SingleOrDefault(w => w.Lemma.Word == "сталь");
            Assert.IsNotNull(first);
            
            Assert.IsTrue(first.Grammatics.Pos == Pos.Noun);
            Assert.IsTrue(first.Grammatics.Gender == Gender.Feminine);
            
            var firstForms = first.ExactForms(word);
            Assert.IsTrue(firstForms.Any(f => f.Grammatics.Number == Number.Plural));
            Assert.IsTrue(firstForms.Any(f => f.Grammatics.Case == Case.Genitive && f.Grammatics.Number == Number.Singular));
            Assert.IsTrue(firstForms.Any(f => f.Grammatics.Case == Case.Accusative && f.Grammatics.Number == Number.Plural));
            Assert.IsTrue(firstForms.Any(f => f.Grammatics.Case == Case.Dative && f.Grammatics.Number == Number.Singular));
            Assert.IsTrue(firstForms.Any(f => f.Grammatics.Case == Case.Prepositional && f.Grammatics.Number == Number.Singular));
            
            // second
            var second = info.SingleOrDefault(w => w.Lemma.Word == "стать");
            Assert.IsNotNull(second);
            
            Assert.IsTrue(second.Grammatics.Pos == Pos.Verb);
        }

        [TearDown]
        public void Dispose()
        {
            _nMorph = null;
        }
    }
}