﻿using System;
using System.Collections.Generic;
using System.Linq;
using Nestor.Models;

namespace Nestor.Poetry
{
    public class RhymeAnalyzer
    {
        private readonly NestorMorph _nestorMorph;
        private static readonly List<string> SoftVowels = new() { "я", "ё", "ю", "и", "е" };
        private static readonly List<string> HardVowels = new() { "а", "о", "у", "ы", "э" };
        
        private static readonly List<string> VoicedConsonants 
            = new() { "б", "Б", "в", "В", "г", "Г", "д", "Д", "ж", "Ж", "з", "З", "л", "Л", "м", "М", "н", "Н", "р", "Р" };
        private static readonly List<string> VoicelessConsonants 
            = new() { "п", "П", "ф", "Ф", "к", "К", "т", "Т", "ш", "Ш", "с", "С" };

        private static readonly Dictionary<string, string> CombinationReplace = new()
        {
            { "тс", "ц" },
            { "тС", "ц" },
            { "Тс", "ц" },
            { "ТС", "ц" },
            { "тч", "ч" },
            { "Тч", "ч" },
            { "Ч", "ч" },
            { "Ц", "ц" },
            { "Ш", "ш" },
            { "Ж", "ж" }
        };

        public RhymeAnalyzer(NestorMorph nestorMorph = null)
        {
            _nestorMorph = nestorMorph ?? new NestorMorph();
        }

        /// <summary>
        /// Find best rhyme between two Russian words
        /// </summary>
        /// <param name="firstWord">First word</param>
        /// <param name="secondWord">Second word</param>
        /// <returns>Rhyme score with best rhyming word forms</returns>
        /// <exception cref="ArgumentException"></exception>
        public RhymingPair ScoreRhyme(string firstWord, string secondWord)
        {
            if (string.IsNullOrEmpty(firstWord) || string.IsNullOrEmpty(secondWord))
            {
                throw new ArgumentException("Both words should not be null");
            }

            WordWithStress[] words1 = GetAllStressedForms(firstWord);
            WordWithStress[] words2 = GetAllStressedForms(secondWord);

            var bestScore = double.MinValue;
            WordWithStress bestForm1 = null;
            WordWithStress bestForm2 = null;
            
            // compare every form with each other and find best score
            foreach (WordWithStress w1 in words1)
            {
                foreach (WordWithStress w2 in words2)
                {
                    double score = SimpleScoreRhyme(w1, w2);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestForm1 = w1;
                        bestForm2 = w2;
                    }
                }
            }

            return new RhymingPair(bestForm1, bestForm2, bestScore);
        }

        /// <summary>
        /// Score rhyme between two Russian words
        /// </summary>
        /// <param name="firstWord">First word with known stress</param>
        /// <param name="secondWord">Second word with known stress</param>
        /// <returns>Rhyme score between 0.0 and 1.0 inclusive, more is better</returns>
        public double SimpleScoreRhyme(WordWithStress firstWord, WordWithStress secondWord)
        {
            // get transcribed tails
            string tail1 = GetTranscription(firstWord.GetTailAfterStress(true));
            string tail2 = GetTranscription(secondWord.GetTailAfterStress(true));

            if (string.IsNullOrEmpty(tail1) || string.IsNullOrEmpty(tail2))
            {
                return 0.0;
            }

            /*Console.WriteLine();
            Console.WriteLine(tail1);
            Console.WriteLine(tail2);*/

            var stressedScore = 0.0;
            var unstressedScore = 0.0;
            var endScore = 0.0;
            
            var idx1 = 0;
            var idx2 = 0;
            var unstressedTrigramsCount = 0;
            var finished = false;
            var stressed = true;
            
            Trigram trigram1 = GetNextTrigram(tail1, ref idx1);
            Trigram trigram2 = GetNextTrigram(tail2, ref idx2);
            
            // score each trigram pair till the end of both words
            while (!finished)
            {
                double pairScore = ScoreTrigramPair(trigram1, trigram2, stressed);
                //Console.WriteLine("{0} {1} = {2}", trigram1, trigram2, pairScore);
                
                // check next
                trigram1 = GetNextTrigram(tail1, ref idx1);
                trigram2 = GetNextTrigram(tail2, ref idx2);
                if (trigram1.IsNull && trigram2.IsNull)
                {
                    finished = true;
                }
                
                // add score
                if (stressed)
                {
                    stressedScore = pairScore;
                    stressed = false;
                    if (finished)
                    {
                        endScore = pairScore;
                    }
                }
                else if (finished)
                {
                    endScore = pairScore;
                }
                else
                {
                    unstressedScore += pairScore;
                    unstressedTrigramsCount++;
                }
            }

            if (unstressedTrigramsCount > 0)
            {
                unstressedScore /= unstressedTrigramsCount;
                return 0.7 * Math.Min(stressedScore, endScore) + 0.1 * unstressedScore + 0.2 * Math.Max(stressedScore, endScore);
            }
            else
            {
                return 0.8 * Math.Min(stressedScore, endScore) + 0.2 * Math.Max(stressedScore, endScore);
            }
        }

        private string GetTranscription(string word)
        {
            List<string> reversed = word.Reverse().Select(c => c.ToString()).ToList();
            var transcribed = new List<string>();
            
            var i = 0;
            var nextConsonantIsSoft = false;
            var nextConsonantIsVoiced = false;
            
            while (i < reversed.Count)
            {
                string letter = reversed[i];
                int softVowelIdx = SoftVowels.FindIndex(v => v == letter);
                if (softVowelIdx >= 0)
                {
                    nextConsonantIsVoiced = true;
                    string hardSibling = HardVowels[softVowelIdx];
                    
                    // soft vowel, see next letter
                    if (i == reversed.Count - 1)
                    {
                        // no next letter, push full transctiption
                        transcribed.Add(hardSibling);
                        transcribed.Add("й");
                    }
                    else
                    {
                        // see next letter
                        string nextLetter = reversed[i + 1];
                        if (IsVowel(nextLetter))
                        {
                            // next letter is vowel, full transctiption
                            transcribed.Add(hardSibling);
                            transcribed.Add("й");
                        }
                        else if (nextLetter is "ь" or "ъ")
                        {
                            // next letter is ь, add full and skip
                            transcribed.Add(hardSibling);
                            transcribed.Add("й");
                        }
                        else
                        {
                            // next letter is consonant, add softness
                            transcribed.Add(hardSibling);
                            nextConsonantIsSoft = true;
                        }
                    }
                } 
                else if (HardVowels.Contains(letter))
                {
                    // hard vowel
                    if (nextConsonantIsSoft)
                    {
                        throw new InvalidOperationException("Next letter cannot be vowel");
                    }

                    nextConsonantIsVoiced = true;
                    transcribed.Add(letter);
                }
                else if (letter == "ь")
                {
                    nextConsonantIsSoft = true;
                    // dont push this letter
                } 
                else if (letter == "ъ")
                {
                    // just skip
                }
                else
                {
                    if (nextConsonantIsSoft)
                    {
                        // simple consonant
                        nextConsonantIsSoft = false;
                        letter = letter.ToUpper();
                    }
                    
                    // test if voiced or voiceless
                    int voicedIndex = VoicedConsonants.FindIndex(c => c == letter);
                    if (voicedIndex >= 0)
                    {
                        if (!nextConsonantIsVoiced && VoicelessConsonants.Count > voicedIndex)
                        {
                            // replace by voiceless if present
                            letter = VoicelessConsonants[voicedIndex];
                        }
                        else
                        {
                            // change next
                            nextConsonantIsVoiced = true;
                        }
                    }
                    else
                    {
                        nextConsonantIsVoiced = false;
                    }

                    transcribed.Add(letter);
                }

                i++;
            }

            string result = string.Join("", transcribed.AsEnumerable().Reverse());

            // special replaces
            foreach ((string key, string value) in CombinationReplace)
            {
                result = result.Replace(key, value);
            }
            
            return result;
        }

        private double ScoreTrigramPair(Trigram firstTrigram, Trigram secondTrigram, bool stressed)
        {
            double leftScore = CompareConsonants(firstTrigram.LeftConsonant, secondTrigram.LeftConsonant);
            double vowelScore = firstTrigram.Vowel == secondTrigram.Vowel ? 1.0 : 0.0;
            double rightScore = CompareConsonants(firstTrigram.RightConsonant, secondTrigram.RightConsonant);
            bool rightIsSilent = firstTrigram.RightIsNull && secondTrigram.RightIsNull
                                 || firstTrigram.RightIsNull && secondTrigram.RightConsonant == "й"
                                 || firstTrigram.RightConsonant == "й" && secondTrigram.RightIsNull;

            return stressed switch
            {
                true when rightIsSilent => 0.8 * Math.Min(leftScore, vowelScore) + 0.2 * Math.Max(leftScore, vowelScore),
                true => 0.15 * leftScore + 0.7 * Math.Min(vowelScore, rightScore) + 0.15 * Math.Max(vowelScore, rightScore),
                false when rightIsSilent => 0.9 * leftScore + 0.1 * vowelScore,
                false => 0.35 * leftScore + 0.05 * vowelScore + 0.6 * rightScore
            };
        }

        private double CompareConsonants(string l1, string l2)
        {
            if (l1 == l2)
            {
                return 1.0;
            }

            if (l1 is null || l2 is null)
            {
                return 0.3;
            } 

            return 0.0;
        }

        private Trigram GetNextTrigram(string word, ref int index)
        {
            var trigram = new Trigram(null, null, null);
            
            // word ended
            if (index >= word.Length)
            {
                return trigram;
            }

            // last letter in word
            if (index == word.Length - 1)
            {
                index++;
                var lastLetter = word[index - 1].ToString();
                return trigram with { LeftConsonant = lastLetter };
            }
            
            // not last letter
            var currentLetter = word[index].ToString();

            // starting from consonant
            if (!IsVowel(currentLetter))
            {
                index++;
                var nextLetter = word[index].ToString();
                while (!IsVowel(nextLetter))
                {
                    index++;
                    // word ends with only consonants, take the last one as trigram
                    if (index >= word.Length)
                    {
                        trigram = trigram with { LeftConsonant = nextLetter };
                        return trigram;
                    }
                    
                    // the end is out there
                    currentLetter = word[index - 1].ToString();
                    nextLetter = word[index].ToString();
                }

                trigram = trigram with { LeftConsonant = currentLetter, Vowel = nextLetter };
            }
            else
            {
                // starting from vowel
                trigram = trigram with { Vowel = currentLetter };
            }
            
            // move to the next
            index++;

            // no next letter
            if (index >= word.Length)
            {
                return trigram;
            }
            
            // take one next letter if this is consonant
            var secondNextLetter = word[index].ToString();
            if (!IsVowel(secondNextLetter))
            {
                trigram = trigram with { RightConsonant = secondNextLetter };
                
                // if this is the last letter of word, stop trigram here
                if (index == word.Length - 1)
                {
                    index++;
                }
            }
            
            // next letter was vowel, stop trigram here
            return trigram;
        }

        private static bool IsVowel(string letter)
        {
            return SoftVowels.Contains(letter) || HardVowels.Contains(letter);
        }

        private WordWithStress[] GetAllStressedForms(string word)
        {
            Word[] info = _nestorMorph.WordInfo(word);
            List<WordForm> forms = info
                .SelectMany(i => i.ExactForms(word))
                .GroupBy(f => f.Stress)
                .Select(g => g.First())
                .Where(f => f.Stress > 0)
                .ToList();

            // known stresses
            if (forms.Count > 0)
            {
                return forms.Select(f => new WordWithStress(f.Word, (byte)f.Stress)).ToArray();
            }
            
            // unknown stress, so take stress for every
            var allVowelsStresses = new List<WordWithStress>();
            byte lastStressNumber = 0;
            foreach (char c in word.Where(NestorMorph.IsVowel))
            {
                lastStressNumber++;
                allVowelsStresses.Add(new WordWithStress(word, lastStressNumber));
            }

            return allVowelsStresses.ToArray();
        }
    }
}