﻿using System.Diagnostics.Contracts;
using JetBrains.ReSharper.FeaturesTestFramework.Intentions;
using NUnit.Framework;
using ReSharper.ContractExtensions.ContextActions;

namespace ReSharper.ContractExtensions.Tests.Postconditions
{
    [TestFixture]
    public class EnsuresContextActionAvailabilityTest : CSharpContextActionAvailabilityTestBase<EnsuresContextAction>
    {
        protected override string RelativeTestDataPath
        {
            get { return "Intentions/ContextActions/Ensures"; }
        }
        protected override string ExtraPath
        {
            get { return "Ensures"; }
        }

        [TestCase("Availability")]
        public void TestSimpleAvailability(string testSrc)
        {
            DoOneTest(testSrc);
        }

        [TestCase("AvailabilityIndexer")]
        public void TestIndexerAvailability(string testSrc)
        {
            DoOneTest(testSrc);
        }

        [TestCase("AvailabilityFull")]
        [TestCase("AvailabilityOnStaticClass")]
        [TestCase("AvailabilityOnInterface")]
        [TestCase("AvailabilityOnAbstractClass")]
        public void TestOtherAvailability(string testSrc)
        {
            DoOneTest(testSrc);
        }
    }
}