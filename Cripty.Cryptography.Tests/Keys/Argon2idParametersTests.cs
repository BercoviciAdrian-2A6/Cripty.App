using Cripty.Cryptography.Keys;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cripty.Cryptography.Tests;

[TestClass]
public sealed class Argon2idParametersTests
{
    [TestMethod]
    public void Recommended_MatchesVersionedSecurityProfile()
    {
        Argon2idParameters parameters =
            Argon2idParameters.Recommended;

        Assert.AreEqual(0x13, parameters.Version);
        Assert.AreEqual(64 * 1024, parameters.MemorySizeKiB);
        Assert.AreEqual(3, parameters.Iterations);
        Assert.AreEqual(4, parameters.DegreeOfParallelism);

        parameters.Validate();
    }

    [TestMethod]
    public void MinimumAllowedValues_AreAccepted()
    {
        CreateParameters(
            memorySizeKiB: 19 * 1024,
            iterations: 2,
            degreeOfParallelism: 1).Validate();
    }

    [TestMethod]
    public void MaximumAllowedValues_AreAccepted()
    {
        CreateParameters(
            memorySizeKiB: 256 * 1024,
            iterations: 10,
            degreeOfParallelism: 16).Validate();
    }

    [DataTestMethod]
    [DataRow(0x12)]
    [DataRow(0x14)]
    public void UnsupportedVersion_IsRejected(int version)
    {
        Argon2idParameters parameters =
            CreateParameters(version: version);

        ArgumentOutOfRangeException exception =
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                parameters.Validate);

        Assert.AreEqual("Version", exception.ParamName);
    }

    [DataTestMethod]
    [DataRow((19 * 1024) - 1)]
    [DataRow((256 * 1024) + 1)]
    public void MemoryOutsideAllowedRange_IsRejected(int memorySizeKiB)
    {
        Argon2idParameters parameters =
            CreateParameters(memorySizeKiB: memorySizeKiB);

        ArgumentOutOfRangeException exception =
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                parameters.Validate);

        Assert.AreEqual("MemorySizeKiB", exception.ParamName);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(11)]
    public void IterationsOutsideAllowedRange_AreRejected(int iterations)
    {
        Argon2idParameters parameters =
            CreateParameters(iterations: iterations);

        ArgumentOutOfRangeException exception =
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                parameters.Validate);

        Assert.AreEqual("Iterations", exception.ParamName);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(17)]
    public void ParallelismOutsideAllowedRange_IsRejected(
        int degreeOfParallelism)
    {
        Argon2idParameters parameters =
            CreateParameters(
                degreeOfParallelism: degreeOfParallelism);

        ArgumentOutOfRangeException exception =
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                parameters.Validate);

        Assert.AreEqual(
            "DegreeOfParallelism",
            exception.ParamName);
    }

    private static Argon2idParameters CreateParameters(
        int version = Argon2idParameters.SupportedVersion,
        int memorySizeKiB = 64 * 1024,
        int iterations = 3,
        int degreeOfParallelism = 4)
    {
        return new Argon2idParameters
        {
            Version = version,
            MemorySizeKiB = memorySizeKiB,
            Iterations = iterations,
            DegreeOfParallelism = degreeOfParallelism
        };
    }
}