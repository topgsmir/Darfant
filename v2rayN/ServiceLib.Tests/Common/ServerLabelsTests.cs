namespace ServiceLib.Tests.Common;

public class ServerLabelsTests
{
    [Test]
    [Arguments("🇩🇪 Frankfurt", null, "DE")]
    [Arguments("آلمان · ایرانسل", null, "DE")]
    [Arguments("US New York", null, "US")]
    [Arguments("Netherlands", "(FI) 192.0.2.1", "FI")]
    [Arguments("server without location", null, null)]
    public async Task Country_ShouldUseAvailableMetadata(string name, string? ipInfo, string? expected)
    {
        await ServerLabels.CountryCode(name, ipInfo).Should().BeEqualTo(expected);
    }

    [Test]
    [Arguments("آلمان · ایرانسل", EServerOperator.Irancell)]
    [Arguments("Germany MTN", EServerOperator.Irancell)]
    [Arguments("DE · همراه‌اول", EServerOperator.HamrahAval)]
    [Arguments("همراه_اول", EServerOperator.HamrahAval)]
    [Arguments("🇩🇪 آلمان · همراه_اول", EServerOperator.HamrahAval)]
    [Arguments("MCI Netherlands", EServerOperator.HamrahAval)]
    [Arguments("MCI MTN", EServerOperator.AllOperators)]
    [Arguments("همه اپراتورها", EServerOperator.AllOperators)]
    [Arguments("همه_اپراتورها", EServerOperator.AllOperators)]
    [Arguments("🇳🇱 هلند · همه_اپراتورها", EServerOperator.AllOperators)]
    [Arguments("all-operators", EServerOperator.AllOperators)]
    [Arguments("Frankfurt", EServerOperator.General)]
    public async Task Operator_ShouldReadExplicitLabels(string name, EServerOperator expected)
    {
        await ServerLabels.DetectOperator(name, null).Should().BeEqualTo(expected);
    }

    [Test]
    [Arguments("همراه_اول", EServerOperator.HamrahAval)]
    [Arguments("همه_اپراتورها", EServerOperator.AllOperators)]
    public async Task Operator_ShouldReadUnderscoresFromSubscriptionName(string subscriptionName, EServerOperator expected)
    {
        var profile = new ProfileItemModel { Remarks = "Frankfurt", SubRemarks = subscriptionName };
        await ServerLabels.GetOperator(profile, new()).Should().BeEqualTo(expected);
    }

    [Test]
    public async Task ManualCategory_ShouldSurviveReimportAndOverrideTheName()
    {
        var profile = new ProfileItemModel { IndexId = "old-id", Subid = "subscription", Address = "example.com", Port = 443, Remarks = "MTN", ConfigType = EConfigType.VLESS };
        var overrides = new Dictionary<string, EServerOperator> { [ServerLabels.ProfileKey(profile)] = EServerOperator.HamrahAval };
        profile.IndexId = "new-id";
        await ServerLabels.GetOperator(profile, overrides).Should().BeEqualTo(EServerOperator.HamrahAval);
        profile.Subid = "another-subscription";
        await ServerLabels.GetOperator(profile, overrides).Should().BeEqualTo(EServerOperator.Irancell);
    }

    [Test]
    public async Task UniversalServers_ShouldAppearInBothMobileCategories()
    {
        await ServerLabels.MatchesOperator(EServerOperator.Irancell, EServerOperator.AllOperators).Should().BeTrue();
        await ServerLabels.MatchesOperator(EServerOperator.HamrahAval, EServerOperator.AllOperators).Should().BeTrue();
        await ServerLabels.MatchesOperator(EServerOperator.AllOperators, EServerOperator.Irancell).Should().BeFalse();
        await ServerLabels.MatchesOperator(EServerOperator.General, EServerOperator.General).Should().BeTrue();
    }
}
