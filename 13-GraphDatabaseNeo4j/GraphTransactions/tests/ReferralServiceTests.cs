using GraphTransactions.Services;
using Neo4j.Driver;

namespace GraphTransactions.Tests;

[Collection("Neo4j")]
public class ReferralServiceTests
{
    private readonly IDriver _driver;
    private readonly IReferralService _sut;

    public ReferralServiceTests(Neo4jFixture fixture)
    {
        _driver = fixture.Driver;
        _sut = new ReferralService(_driver);
    }

    [Fact]
    public async Task ReferAsync_creates_all_three_writes_together()
    {
        var referrerId = $"person-{Guid.NewGuid()}";
        var newHireId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        await GraphTestHelpers.CreatePersonAsync(_driver, referrerId, "Referrer");
        await GraphTestHelpers.CreatePersonAsync(_driver, newHireId, "NewHire");
        await GraphTestHelpers.CreateCompanyAsync(_driver, companyId, "Acme");

        var result = await _sut.ReferAsync(referrerId, newHireId, companyId, "Engineer");

        result.ReferrerReferralCount.Should().Be(1);
        (await GraphTestHelpers.WorksAtExistsAsync(_driver, newHireId, companyId)).Should().BeTrue();
        (await GraphTestHelpers.KnowsCountAsync(_driver, referrerId, newHireId)).Should().Be(1);
        (await GraphTestHelpers.GetReferralCountAsync(_driver, referrerId)).Should().Be(1);
    }

    [Fact]
    public async Task ReferAsync_reuses_an_existing_KNOWS_relationship_instead_of_duplicating_it()
    {
        var referrerId = $"person-{Guid.NewGuid()}";
        var newHireId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";
        var secondCompanyId = $"company-{Guid.NewGuid()}";

        await GraphTestHelpers.CreatePersonAsync(_driver, referrerId, "Referrer");
        await GraphTestHelpers.CreatePersonAsync(_driver, newHireId, "NewHire");
        await GraphTestHelpers.CreateCompanyAsync(_driver, companyId, "Acme");
        await GraphTestHelpers.CreateCompanyAsync(_driver, secondCompanyId, "Globex");

        await _sut.ReferAsync(referrerId, newHireId, companyId, "Engineer");
        await _sut.ReferAsync(referrerId, newHireId, secondCompanyId, "Consultant");

        (await GraphTestHelpers.KnowsCountAsync(_driver, referrerId, newHireId)).Should().Be(1);
        (await GraphTestHelpers.GetReferralCountAsync(_driver, referrerId)).Should().Be(2);
    }

    [Fact]
    public async Task ReferAsync_simulated_failure_before_the_increment_rolls_back_all_three_writes()
    {
        var referrerId = $"person-{Guid.NewGuid()}";
        var newHireId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        await GraphTestHelpers.CreatePersonAsync(_driver, referrerId, "Referrer");
        await GraphTestHelpers.CreatePersonAsync(_driver, newHireId, "NewHire");
        await GraphTestHelpers.CreateCompanyAsync(_driver, companyId, "Acme");

        var act = async () => await _sut.ReferAsync(
            referrerId, newHireId, companyId, "Engineer", simulateFailureBeforeIncrement: true);

        await act.Should().ThrowAsync<InvalidOperationException>();

        (await GraphTestHelpers.WorksAtExistsAsync(_driver, newHireId, companyId)).Should().BeFalse(
            "the WORKS_AT write happened before the simulated failure but must still roll back");
        (await GraphTestHelpers.KnowsCountAsync(_driver, referrerId, newHireId)).Should().Be(0,
            "the KNOWS write happened before the simulated failure but must still roll back");
        (await GraphTestHelpers.GetReferralCountAsync(_driver, referrerId)).Should().Be(0,
            "the referralCount increment never ran, and the two writes before it must not survive either");
    }

    [Fact]
    public async Task ReferAsync_against_a_nonexistent_company_commits_without_creating_a_WORKS_AT_relationship()
    {
        var referrerId = $"person-{Guid.NewGuid()}";
        var newHireId = $"person-{Guid.NewGuid()}";
        var missingCompanyId = $"company-{Guid.NewGuid()}";

        await GraphTestHelpers.CreatePersonAsync(_driver, referrerId, "Referrer");
        await GraphTestHelpers.CreatePersonAsync(_driver, newHireId, "NewHire");

        // Cypher's MATCH simply finds zero rows here -- it does not throw.
        // The referralCount increment and the KNOWS write still run because
        // their own MATCH clauses succeed independently; only the WORKS_AT
        // write's MERGE has nothing to attach to.
        var result = await _sut.ReferAsync(referrerId, newHireId, missingCompanyId, "Engineer");

        result.ReferrerReferralCount.Should().Be(1);
        (await GraphTestHelpers.WorksAtExistsAsync(_driver, newHireId, missingCompanyId)).Should().BeFalse();
        (await GraphTestHelpers.KnowsCountAsync(_driver, referrerId, newHireId)).Should().Be(1);
    }

    [Fact]
    public async Task ReferAsync_called_sequentially_twice_increments_referralCount_each_time()
    {
        var referrerId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";
        await GraphTestHelpers.CreatePersonAsync(_driver, referrerId, "Referrer");
        await GraphTestHelpers.CreateCompanyAsync(_driver, companyId, "Acme");

        var firstHireId = $"person-{Guid.NewGuid()}";
        var secondHireId = $"person-{Guid.NewGuid()}";
        await GraphTestHelpers.CreatePersonAsync(_driver, firstHireId, "Hire1");
        await GraphTestHelpers.CreatePersonAsync(_driver, secondHireId, "Hire2");

        var first = await _sut.ReferAsync(referrerId, firstHireId, companyId, "Engineer");
        var second = await _sut.ReferAsync(referrerId, secondHireId, companyId, "Designer");

        first.ReferrerReferralCount.Should().Be(1);
        second.ReferrerReferralCount.Should().Be(2);
    }
}
