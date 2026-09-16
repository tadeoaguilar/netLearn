using FunctionApi.Notes;

namespace FunctionApi.Tests;

public class InMemoryNotesStoreTests
{
    [Fact]
    public void GetAll_is_empty_for_a_fresh_store()
    {
        var store = new InMemoryNotesStore();

        store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Add_returns_and_persists_the_note_with_an_incrementing_id()
    {
        var store = new InMemoryNotesStore();

        var first = store.Add("first note", "caller-a");
        var second = store.Add("second note", "caller-b");

        second.Id.Should().BeGreaterThan(first.Id);
        store.GetAll().Should().HaveCount(2)
            .And.Contain(n => n.Text == "first note" && n.CreatedBy == "caller-a")
            .And.Contain(n => n.Text == "second note" && n.CreatedBy == "caller-b");
    }

    [Fact]
    public async Task Add_is_safe_to_call_concurrently()
    {
        var store = new InMemoryNotesStore();

        await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => store.Add($"note {i}", "caller"))));

        store.GetAll().Should().HaveCount(50);
        store.GetAll().Select(n => n.Id).Distinct().Should().HaveCount(50);
    }
}
