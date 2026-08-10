using SilksongTweaks.Rules;
using Xunit;

namespace SilksongTweaks.Tests;

public class DamageRulesTests
{
    [Theory]
    [InlineData(1, 1.0f, 1)]
    [InlineData(2, 1.0f, 2)]
    [InlineData(2, 0.5f, 1)]
    [InlineData(4, 0.25f, 1)]
    [InlineData(2, 2.0f, 4)]
    public void Scales_damage_by_multiplier(int incoming, float multiplier, int expected) =>
        Assert.Equal(expected, DamageRules.Scale(incoming, multiplier));

    [Fact]
    public void A_real_hit_never_silently_becomes_zero()
    {
        // 1 damage at 10% would round to 0. A hit that deals nothing but still knocks you back
        // and burns i-frames reads as a broken game, so it floors at 1 instead.
        Assert.Equal(1, DamageRules.Scale(1, 0.1f));
        Assert.Equal(1, DamageRules.Scale(2, 0.01f));
    }

    [Fact]
    public void Zero_multiplier_is_the_one_way_to_take_no_damage() =>
        Assert.Equal(0, DamageRules.Scale(5, 0f));

    [Fact]
    public void Negative_multiplier_cannot_heal_you() =>
        Assert.Equal(0, DamageRules.Scale(5, -3f));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_damage_passes_through_untouched(int incoming) =>
        Assert.Equal(incoming, DamageRules.Scale(incoming, 0.5f));
}

public class HealthRulesTests
{
    [Fact]
    public void Disabled_returns_the_vanilla_value() =>
        Assert.Equal(5, HealthRules.Resolve(vanillaMax: 5, configuredMasks: 20, enabled: false));

    [Fact]
    public void Raises_max_health_to_the_configured_value() =>
        Assert.Equal(12, HealthRules.Resolve(vanillaMax: 5, configuredMasks: 12, enabled: true));

    [Fact]
    public void Never_makes_you_weaker_than_vanilla()
    {
        // Collected mask shards must not be undone by a low slider — otherwise upgrading in-game
        // would appear to do nothing, which is worse than the mod simply being off.
        Assert.Equal(9, HealthRules.Resolve(vanillaMax: 9, configuredMasks: 3, enabled: true));
    }

    [Theory]
    [InlineData(0, HealthRules.MinMasks)]
    [InlineData(-5, HealthRules.MinMasks)]
    [InlineData(999, HealthRules.MaxMasks)]
    public void Clamps_configured_value_into_range(int configured, int expected) =>
        Assert.Equal(expected, HealthRules.Resolve(vanillaMax: 1, configured, enabled: true));
}

public class MoneyRulesTests
{
    [Theory]
    [InlineData(10, 1.0f, 10)]
    [InlineData(10, 2.0f, 20)]
    [InlineData(7, 3.0f, 21)]
    public void Multiplies_the_pickup(int amount, float multiplier, int expected) =>
        Assert.Equal(expected, MoneyRules.Scale(amount, multiplier));

    [Fact]
    public void Rounds_rather_than_truncating()
    {
        // 1 rosary at 1.5x truncates to 1, which reads as "the setting does nothing".
        Assert.Equal(2, MoneyRules.Scale(1, 1.5f));
        Assert.Equal(5, MoneyRules.Scale(3, 1.5f));
    }

    [Fact]
    public void A_multiplier_can_never_reduce_a_pickup()
    {
        Assert.Equal(10, MoneyRules.Scale(10, 0.5f));
        Assert.Equal(10, MoneyRules.Scale(10, -4f));
    }

    [Fact]
    public void Clamps_at_the_maximum() =>
        Assert.Equal(100, MoneyRules.Scale(10, 999f));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_amounts_pass_through(int amount) =>
        Assert.Equal(amount, MoneyRules.Scale(amount, 5f));

    [Fact]
    public void Huge_pickups_cannot_overflow() =>
        Assert.Equal(int.MaxValue, MoneyRules.Scale(int.MaxValue, 10f));
}

public class CocoonRulesTests
{
    [Fact]
    public void Keeping_everything_adds_the_previous_pool_to_the_new_one() =>
        Assert.Equal(115, CocoonRules.Merge(previousPool: 100, carriedThisDeath: 15, keepFraction: 1f));

    [Fact]
    public void A_zero_fraction_reproduces_vanilla_exactly()
    {
        // Vanilla destroys the previous cocoon, so the new pool is only what you were carrying.
        Assert.Equal(15, CocoonRules.Merge(previousPool: 100, carriedThisDeath: 15, keepFraction: 0f));
    }

    [Fact]
    public void A_partial_fraction_keeps_a_proportion_of_the_old_pool() =>
        Assert.Equal(90, CocoonRules.Merge(previousPool: 100, carriedThisDeath: 15, keepFraction: 0.75f));

    [Fact]
    public void First_death_with_no_previous_pool_just_carries_what_you_held() =>
        Assert.Equal(15, CocoonRules.Merge(previousPool: 0, carriedThisDeath: 15, keepFraction: 1f));

    [Fact]
    public void Fractions_above_one_are_clamped_and_cannot_print_money() =>
        Assert.Equal(115, CocoonRules.Merge(previousPool: 100, carriedThisDeath: 15, keepFraction: 5f));

    [Fact]
    public void Repeated_deaths_at_full_keep_never_overflow()
    {
        var pool = CocoonRules.Merge(int.MaxValue, carriedThisDeath: 1000, keepFraction: 1f);
        Assert.Equal(int.MaxValue, pool);
    }

    [Theory]
    [InlineData(-10, 5, 5)]
    [InlineData(10, -5, 10)]
    public void Negative_inputs_are_treated_as_zero(int previous, int carried, int expected) =>
        Assert.Equal(expected, CocoonRules.Merge(previous, carried, keepFraction: 1f));
}
