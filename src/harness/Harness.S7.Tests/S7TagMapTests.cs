using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// The tag map is the only place the symbolic world meets the byte-offset world, and it is
/// hand-written against a DB layout read off a screen. Every check here exists because the
/// corresponding mistake produces plausible data rather than an error.
/// </summary>
public class S7TagMapTests
{
    [Fact]
    public void Two_tags_with_the_same_name_are_refused()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() => new S7TagMap(new[]
        {
            new S7Tag("Level", "DB_A", 1, 0, S7DataType.Real),
            new S7Tag("level", "DB_A", 1, 4, S7DataType.Real),
        }));

        Assert.Contains("defined twice", ex.Message);
    }

    [Fact]
    public void Overlapping_tags_are_refused_because_writing_one_would_change_the_other()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() => new S7TagMap(new[]
        {
            new S7Tag("Level", "DB_A", 1, 4, S7DataType.Real),
            new S7Tag("Count", "DB_A", 1, 6, S7DataType.Int),
        }));

        Assert.Contains("overlapping bits", ex.Message);
    }

    [Fact]
    public void Bools_sharing_a_byte_at_different_bits_are_perfectly_normal()
    {
        var map = new S7TagMap(new[]
        {
            new S7Tag("Start", "DB_A", 1, 0, S7DataType.Bool, 0),
            new S7Tag("Stop", "DB_A", 1, 0, S7DataType.Bool, 1),
            new S7Tag("Reset", "DB_A", 1, 0, S7DataType.Bool, 7),
        });

        Assert.Equal(3, map.Tags.Count);
    }

    [Fact]
    public void Two_bools_at_the_same_bit_are_still_an_overlap()
    {
        Assert.Throws<S7ConfigurationException>(() => new S7TagMap(new[]
        {
            new S7Tag("Start", "DB_A", 1, 0, S7DataType.Bool, 3),
            new S7Tag("Go", "DB_A", 1, 0, S7DataType.Bool, 3),
        }));
    }

    [Fact]
    public void The_same_offsets_in_different_dbs_do_not_overlap()
    {
        var map = new S7TagMap(new[]
        {
            new S7Tag("A", "DB_A", 1, 0, S7DataType.Real),
            new S7Tag("B", "DB_B", 2, 0, S7DataType.Real),
        });

        Assert.Equal(2, map.Tags.Count);
    }

    [Fact]
    public void A_word_sized_tag_at_an_odd_offset_is_refused()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() =>
            new S7TagMap(new[] { new S7Tag("Level", "DB_A", 1, 3, S7DataType.Real) }));

        Assert.Contains("plausible-looking garbage", ex.Message);
    }

    [Fact]
    public void A_bit_offset_on_a_non_bool_is_refused()
    {
        Assert.Throws<S7ConfigurationException>(() =>
            new S7TagMap(new[] { new S7Tag("Level", "DB_A", 1, 0, S7DataType.Real, 2) }));
    }

    [Fact]
    public void A_bit_offset_outside_zero_to_seven_is_refused()
    {
        Assert.Throws<S7ConfigurationException>(() =>
            new S7TagMap(new[] { new S7Tag("Flag", "DB_A", 1, 0, S7DataType.Bool, 8) }));
    }

    [Fact]
    public void A_tag_with_no_area_is_refused_because_the_fence_is_scoped_on_areas()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() =>
            new S7TagMap(new[] { new S7Tag("Flag", "", 1, 0, S7DataType.Bool) }));

        Assert.Contains("write fence is scoped on", ex.Message);
    }

    [Fact]
    public void Areas_lists_the_vocabulary_the_write_scope_must_use()
    {
        var map = new S7TagMap(new[]
        {
            new S7Tag("A", "DB_Two", 1, 0, S7DataType.Real),
            new S7Tag("B", "DB_One", 2, 0, S7DataType.Real),
            new S7Tag("C", "DB_One", 2, 4, S7DataType.Real),
        });

        Assert.Equal(new[] { "DB_One", "DB_Two" }, map.Areas);
    }

    [Fact]
    public void Json_round_trips()
    {
        var map = S7TagMap.FromJson("""
            {
              "tags": [
                { "name": "Cmd_Start", "area": "DB_Interface", "db": 10, "byte": 0, "type": "Bool", "bit": 3 },
                { "name": "Setpoint",  "area": "DB_Interface", "db": 10, "byte": 2, "type": "Real" }
              ]
            }
            """);

        var start = map.Resolve("cmd_start");
        Assert.Equal(S7DataType.Bool, start.Type);
        Assert.Equal(3, start.BitOffset);
        Assert.Equal(S7DataType.Real, map.Resolve("Setpoint").Type);
    }

    [Fact]
    public void An_unknown_type_names_the_ones_that_exist()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() => S7TagMap.FromJson(
            """{ "tags": [ { "name": "X", "area": "A", "db": 1, "byte": 0, "type": "LReal" } ] }"""));

        Assert.Contains("UDInt", ex.Message);
    }

    [Fact]
    public void An_empty_map_is_refused_rather_than_producing_confusing_unknown_tag_errors_later()
    {
        Assert.Throws<S7ConfigurationException>(() => S7TagMap.FromJson("""{ "tags": [] }"""));
    }

    [Fact]
    public void Resolving_an_unknown_tag_explains_why_it_is_not_an_empty_read()
    {
        var map = new S7TagMap(new[] { new S7Tag("A", "DB_A", 1, 0, S7DataType.Real) });

        var ex = Assert.Throws<S7ConfigurationException>(() => map.Resolve("B"));
        Assert.Contains("empty expectation", ex.Message);
    }
}
