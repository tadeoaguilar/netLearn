namespace GraphTransactions.Domain;

public class Person
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    // Bumped every time this person successfully refers someone else --
    // see Services/ReferralService.cs. Defaults to zero for a brand-new
    // Person node.
    public int ReferralCount { get; set; }
}
