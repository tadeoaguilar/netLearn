# Exercise: Graph Algorithms with Neo4j Graph Data Science

## Overview
In this exercise, you'll use the Neo4j **Graph Data Science (GDS)** library to answer questions plain Cypher traversal can't answer well: *who is the most influential person in this network?*, *what friend groups actually exist?*, and *which two people are most alike?* You'll seed a small professional network, project it into GDS's optimized in-memory format, and run three real graph algorithms against it: PageRank, Louvain, and Node Similarity.

## Learning Goals
By completing this exercise, you will:
- Understand what makes GDS different from the Cypher traversals you used in `GraphTraversals`
- Project a named graph into the GDS catalog, and understand its lifecycle
- Run PageRank to rank people by influence, not just by follower count
- Run Louvain to detect communities (friend groups) from connection density
- Run Node Similarity to build a "people you may know" recommendation

---

## Part 1: Why GDS Is a Different Execution Model

### The problem with plain Cypher for this kind of question

In `GraphTraversals`, every query started from a known node and walked outward: "find Alice's friends-of-friends," "find the shortest path from Alice to Zara." Cypher's query planner is built for exactly that -- start somewhere, expand along relationships, stop when you've gone far enough.

PageRank, community detection, and similarity scoring don't start anywhere. They need to look at the **entire graph's structure at once** -- every node, every relationship, potentially many passes over all of it -- to produce an answer. "Who is most influential?" isn't a traversal from a starting point; it's a property that emerges from the whole network's shape. Running that kind of iterative, whole-graph computation as a live Cypher traversal, hitting the transactional storage layer on every step, would be far too slow.

### GDS's answer: project first, then compute

GDS solves this by splitting the work into two steps:

1. **Project** -- copy the *relevant slice* of your graph (which node labels, which relationship types) into a compact, in-memory columnar format, purpose-built for numerical graph algorithms.
2. **Compute** -- run an algorithm against that projection. Because it's already in memory in an algorithm-friendly layout, GDS can do the repeated whole-graph passes these algorithms need without touching the transactional store again.

This is the key mental model shift: **GDS algorithms never run against your live graph directly.** They run against a snapshot you explicitly created.

**Your Task:**
Before writing any code, start the container for this project (see `GETTING_STARTED.md`) and confirm GDS actually loaded, since this container takes noticeably longer to start than the others in this module (the plugin has to download and initialize):

```cypher
RETURN gds.version();
```

Run that in Neo4j Browser at `http://localhost:7477`, or from the driver -- you'll do the driver version in Part 3's setup code. If it returns a version string, you're ready.

**Questions to think about:**
1. Why does an algorithm like PageRank need to see the whole graph, while a traversal like "find Alice's 2nd-degree connections" only needs to see a small neighborhood around one node?
2. What do you think happens to a GDS computation's accuracy if you project only *half* of the relevant relationships?

---

## Part 2: Projecting a Named Graph

### Step 2.1: Seed the graph

**Your Task:**
Create `Seeding/GraphSeeder.cs` in your workspace project and write an idempotent seed method (use `MERGE` throughout) that creates:
- 12 `Person` nodes
- A few `Company` nodes with `WORKS_AT` relationships
- `KNOWS` relationships forming **three distinct, densely-connected clusters** of people, joined by only one or two bridging edges between clusters
- `FOLLOWS` relationships (directed) where **one person has far more incoming follows than everyone else**, and **at least two people follow an identical set of other people** (for Part 5)

This is the same professional-network shape as `GraphModeling`, `GraphQuerying`, and `GraphTraversals` -- `Person`, `Company`, `KNOWS {since}`, `WORKS_AT {role, since}`, `FOLLOWS` -- but the *data* needs deliberate structure this time. A random graph won't give PageRank, Louvain, or Node Similarity anything interesting to find.

**Why:** Every algorithm in this exercise needs graph structure that actually reflects the property it's measuring. If you seed a random graph, PageRank's "most influential" answer will be arbitrary noise, Louvain won't find clean communities, and every similarity score will look about the same. Designing the data on purpose is what makes the results legible.

### Step 2.2: Project the FOLLOWS graph

**Your Task:**
In `Program.cs`, after seeding, project a named graph:

```csharp
await using var session = driver.AsyncSession();

await session.ExecuteWriteAsync(async tx =>
{
    var cursor = await tx.RunAsync(
        "CALL gds.graph.project($graphName, 'Person', {FOLLOWS: {orientation: 'NATURAL'}})",
        new { graphName = "social-network" });
    await cursor.ConsumeAsync();
});

Console.WriteLine("Projected 'social-network'.");
```

> **Note on the exact signature:** GDS 2.x's `gds.graph.project` accepts a node projection and a relationship projection, each as either a simple label/type string or a configuration map (as used above, to set `orientation`). The shape here matches the documented 2.x procedure signature; if your GDS version's exact return columns differ, `YIELD` and inspect them with `CALL gds.graph.project(...) YIELD graphName, nodeCount, relationshipCount` to see what came back.

### Step 2.3: Understand the catalog lifecycle

**Your Task:**
Run this **twice** and see what happens the second time:

```cypher
CALL gds.graph.project('social-network', 'Person', {FOLLOWS: {orientation: 'NATURAL'}})
```

**Observe:** The second call fails -- a graph named `social-network` already exists in the catalog. That's the gotcha to internalize:

> A projected graph is a **separate, named object** in GDS's in-memory catalog. It persists there until you explicitly drop it (`CALL gds.graph.drop('social-network', false)`), **independently of the live graph in the database.** If you write more `Person` or `FOLLOWS` data after projecting, your projection is now stale -- algorithms will run against the old snapshot until you drop and re-project.

This project's `GraphCatalog` helper (in the solution) drops-then-reprojects every time, specifically to sidestep this trap.

**Questions to think about:**
1. If you seed 5 more people and re-run PageRank without re-projecting, what result would you get, and why would it be wrong?
2. Why might GDS choose to make projections explicit and persistent, rather than silently re-projecting fresh data on every algorithm call?

---

## Part 3: PageRank -- Who's Actually Influential?

**Your Task:**
Run PageRank against the graph you projected in Part 2, and join the results back to `Person.name`:

```cypher
CALL gds.pageRank.stream('social-network')
YIELD nodeId, score
RETURN gds.util.asNode(nodeId).name AS name, score
ORDER BY score DESC
```

From C#, wrap this in a read transaction and print the top 5 names with their scores.

**Why:** PageRank does **not** just count incoming edges. It measures influence recursively: a node's score depends on the scores of the nodes pointing to it, so being followed by a handful of *already-influential* people counts for more than being followed by many low-influence ones. Two people can have the same number of followers and end up with very different PageRank scores, depending on who those followers are.

**Questions to think about:**
1. Look at your seed data: who has the most incoming `FOLLOWS`? Does PageRank's top result match your expectation, or did the recursive weighting change the ranking?
2. What would happen to Alice's score if one of her followers followed nobody else vs. if that same follower were also followed by five other people?

---

## Part 4: Louvain -- Finding Friend Groups

**Your Task:**
Drop `social-network`, and project a *different* named graph on `KNOWS` instead, this time undirected (friendship has no inherent direction):

```cypher
CALL gds.graph.project('friend-groups', 'Person', {KNOWS: {orientation: 'UNDIRECTED'}})
```

Then run Louvain and group the results by community:

```cypher
CALL gds.louvain.stream('friend-groups')
YIELD nodeId, communityId
RETURN gds.util.asNode(nodeId).name AS name, communityId
ORDER BY communityId, name
```

In C#, group the rows by `communityId` and print each community's members together.

**Why:** Louvain detects communities by maximizing *modularity* -- it looks for groups of nodes that are much more densely connected to each other than to the rest of the graph. It doesn't need you to tell it how many communities to look for or where the boundaries are; it finds them from the connection density alone.

**Questions to think about:**
1. Does the number of communities Louvain finds match the number of clusters you deliberately seeded? If not, look at your bridging `KNOWS` edges -- are they sparse enough?
2. Why did this part use `KNOWS` (undirected) instead of `FOLLOWS` (directed)? What would community detection even mean on a directed "who follows whom" graph?

---

## Part 5: Node Similarity -- "People You May Know"

**Your Task:**
Re-project `social-network` (it's the same query as Part 2 -- notice you have to do this again, because you dropped it in Part 4's cleanup, or because your session's projection may be stale). Then run Node Similarity:

```cypher
CALL gds.nodeSimilarity.stream('social-network')
YIELD node1, node2, similarity
RETURN gds.util.asNode(node1).name AS personA,
       gds.util.asNode(node2).name AS personB,
       similarity
ORDER BY similarity DESC
```

Print the top few pairs and their similarity scores.

**Why:** Node Similarity computes the **Jaccard coefficient** between each pair of nodes' relationship sets: `|shared connections| / |union of connections|`. Two people who follow an almost-identical set of other people score close to 1.0, even if they've never interacted with each other directly -- which is exactly the "people you may know" recommendation pattern real social platforms use.

**Questions to think about:**
1. Find the pair you deliberately gave an identical follow-set in Part 2.1. Is their score at (or very near) 1.0?
2. Two people who share **zero** followed accounts -- what similarity score would you expect, and does GDS even include that pair in the stream output?

---

## Reflection Questions

After completing this exercise, answer these:

1. **What's the fundamental difference between how GDS executes an algorithm and how a Cypher `MATCH` traversal executes?**
2. **Why is a projected graph's lifecycle independent of the live database, and what bug could that cause if you forget about it?**
3. **PageRank vs. plain in-degree**: describe a concrete scenario (in this dataset or your own) where they'd rank two people differently.
4. **Louvain found communities without being told how many to look for. What's the tradeoff of an algorithm that infers structure like this, versus one where you specify `k` clusters up front?**
5. **Node Similarity's "people you may know" is a Jaccard coefficient on shared relationships. What's a different real-world recommendation this same algorithm/pattern could power, if you changed which relationship type you projected?**

---

## Summary

You've learned:
- Why whole-graph algorithms need a different execution model than live traversal
- How to project a named graph into the GDS catalog, and its independent lifecycle
- PageRank for influence ranking (not just in-degree)
- Louvain for community detection by modularity
- Node Similarity (Jaccard) for "people you may know" recommendations

## Next Steps

This is the last of the five Neo4j projects in this module. If you haven't already, go back through `GraphModeling`, `GraphQuerying`, `GraphTraversals`, and `GraphTransactions` to see how the same professional-network data model supports very different querying and processing techniques.
