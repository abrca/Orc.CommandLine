// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CommandLineParserFacts.cs" company="Wild Gums">
//   Copyright (c) 2008 - 2015 Wild Gums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.CommandLine.Tests
{
    using System;
    using Context;
    using NUnit.Framework;

    [TestFixture]
    public class CommandLineParserFacts
    {
        #region Methods
        private ICommandLineParser CreateCommandLineParser()
        {
            return new CommandLineParser(new OptionDefinitionService());
        }

        [TestCase("somefile", "false", "0", "", "somefile")]
        [TestCase("somefile /b /s somestring /i 42", "true", "42", "somestring", "somefile")]
        public void CorrectlyParsesCommandLinesWithFile(string input, string expectedBooleanSwitch, string expectedIntegerSwitch,
            string expectedStringSwitch, string expectedFileName)
        {
            var commandLineParser = CreateCommandLineParser();

            var context = new TestContextWithFile();
            var validationContext = commandLineParser.Parse(input, context);

            Assert.IsFalse(validationContext.HasErrors);
            Assert.IsFalse(validationContext.HasWarnings);

            Assert.IsTrue(string.Equals(expectedBooleanSwitch, context.BooleanSwitch.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(string.Equals(expectedIntegerSwitch, context.IntegerSwitch.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(string.Equals(expectedStringSwitch, context.StringSwitch));
            Assert.IsTrue(string.Equals(expectedFileName, context.FileName));
        }

        [TestCase("-h")]
        [TestCase("/h")]
        [TestCase("-help")]
        [TestCase("/help")]
        [TestCase("-?")]
        [TestCase("/?")]
        [TestCase("somefile -h")]
        [TestCase("somefile /b /s somestring /i 42 /help")]
        [TestCase("somefile /b /s somestring /i 42 -?")]
        public void CorrectlyParsesCommandLineWithHelp(string input)
        {
            var commandLineParser = CreateCommandLineParser();

            var context = new TestContextWithFile();
            var validationContext = commandLineParser.Parse(input, context);

            Assert.IsFalse(validationContext.HasErrors);
            Assert.IsFalse(validationContext.HasWarnings);

            Assert.IsTrue(context.IsHelp);
        }
        #endregion
    }
}