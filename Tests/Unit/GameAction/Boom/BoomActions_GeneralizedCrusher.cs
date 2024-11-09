using FluentAssertions;
using Helion.World.Physics;
using Xunit;

namespace Helion.Tests.Unit.GameAction.Boom;

public partial class BoomActions
{
    [Fact(DisplayName = "Generalized crusher slow")]
    public void GeneralizedCrusherSlow()
    {
        var sector = GameActions.GetSectorByTag(World, 50);
        GameActions.ActivateLineByTag(World, Player, 50, ActivationContext.CrossLine).Should().BeTrue();
        GameActions.RunCrusherCeiling(World, sector, 8, slowDownOnCrush: true);
    }

    [Fact(DisplayName = "Generalized crusher normal")]
    public void GeneralizedCrusherNormal()
    {
        var sector = GameActions.GetSectorByTag(World, 51);
        GameActions.ActivateLineByTag(World, Player, 51, ActivationContext.CrossLine).Should().BeTrue();
        GameActions.RunCrusherCeiling(World, sector, 16, slowDownOnCrush: true);
    }

    [Fact(DisplayName = "Generalized crusher fast")]
    public void GeneralizedCrusherFast()
    {
        var sector = GameActions.GetSectorByTag(World, 52);
        GameActions.ActivateLineByTag(World, Player, 52, ActivationContext.CrossLine).Should().BeTrue();
        GameActions.RunCrusherCeiling(World, sector, 32, slowDownOnCrush: false, assertDeadEntities: false);
    }

    [Fact(DisplayName = "Generalized crusher turbo")]
    public void GeneralizedCrusherTurbo()
    {
        var sector = GameActions.GetSectorByTag(World, 53);
        GameActions.ActivateLineByTag(World, Player, 53, ActivationContext.CrossLine).Should().BeTrue();
        GameActions.RunCrusherCeiling(World, sector, 64, slowDownOnCrush: false, assertDeadEntities: false);
    }
}
