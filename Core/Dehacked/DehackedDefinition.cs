using Helion.Util;
using Helion.Util.Container;
using Helion.Util.Extensions;
using Helion.Util.Parser;
using Helion.World.Entities.Definition;
using Helion.World.Entities.Definition.Composer;
using Helion.World.Entities.Definition.States;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Helion.Dehacked;

public partial class DehackedDefinition
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly Regex PointerRegex = new(@"^\(\S+ (\d+)\)");

    public readonly List<DehackedThing> Things = new();
    public readonly List<DehackedFrame> Frames = new();
    public readonly List<DehackedAmmo> Ammo = new();
    public readonly List<DehackedWeapon> Weapons = new();
    public readonly List<DehackedString> Strings = new();
    public readonly List<DehackedPointer> Pointers = new();
    public readonly List<DehackedSound> Sounds = new();

    public readonly List<BexString> BexStrings = new();
    public readonly List<BexPar> BexPars = new();
    public readonly List<BexItem> BexSounds = new();
    public readonly List<BexItem> BexSprites = new();

    public readonly Dictionary<int, string> NewSoundLookup = new();
    public readonly Dictionary<int, string> NewSpriteLookup = new();
    public readonly LookupArray<EntityDefinition> NewThingLookup = new();
    public readonly Dictionary<int, EntityFrame> NewEntityFrameLookup = new();
    public readonly EntityDefinition?[] ActorDefinitions;

    private readonly StringBuilder m_sb = new();

    public DehackedCheat? Cheat { get; private set; }
    public DehackedMisc? Misc { get; private set; }
    public int DoomVersion { get; private set; }
    public int PatchFormat { get; set; }

    public DehackedDefinition()
    {
        ActorDefinitions = new EntityDefinition[ActorNames.Length];
    }

    public void LoadActorDefinitions(EntityDefinitionComposer composer)
    {
        for (int i = 0; i < ActorNames.Length; i++)
            ActorDefinitions[i] = composer.GetByName(ActorNames[i]);
    }

    public void Parse(string data)
    {
        data = data.Replace('\0', ' ').StripNonUtf8Chars();
        SimpleParser parser = CreateDehackedParser(data);
        parser = ParseHeader(parser, data);

        while (!parser.IsDone())
        {
            string item = parser.PeekString();
            if (item.StartsWith('#'))
            {
                parser.ConsumeLine();
                continue;
            }

            int itemLine = parser.GetCurrentLine();
            if (BaseTypes.Contains(item))
                parser.ConsumeString();

            if (item.EqualsIgnoreCase(ThingName))
                ParseThing(parser);
            else if (item.EqualsIgnoreCase(FrameName))
                ParseFrame(parser);
            else if (item.EqualsIgnoreCase(AmmoName))
                ParseAmmo(parser);
            else if (item.EqualsIgnoreCase(WeaponName))
                ParseWeapon(parser);
            else if (item.EqualsIgnoreCase(CheatName))
                ParseCheat(parser);
            else if (item.EqualsIgnoreCase(TextName))
                ParseText(parser);
            else if (item.EqualsIgnoreCase(PointerName))
                ParsePointer(parser);
            else if (item.StartsWith(MiscName, StringComparison.OrdinalIgnoreCase))
                ParseMisc(parser, itemLine);
            else if (item.EqualsIgnoreCase(SoundName))
                ParseSound(parser);
            else if (item.EqualsIgnoreCase(BexStringName))
                ParseBexString(parser);
            else if (item.EqualsIgnoreCase(BexPointerName))
                ParseBexPointer(parser);
            else if (item.EqualsIgnoreCase(BexParName))
                ParseBexPar(parser);
            else if (item.EqualsIgnoreCase(BexSoundName))
                ParseBexItem(parser, BexSounds);
            else if (item.EqualsIgnoreCase(BexSpriteName))
                ParseBexItem(parser, BexSprites);
            else if (IsUselessLine(item))
                parser.ConsumeLine();
            else
                UnknownWarning(parser, "type", item);

            ConsumeLine(parser, itemLine);
        }
    }

    private static readonly char[] SpecialChars = ['='];

    private static SimpleParser CreateDehackedParser(string data)
    {
        SimpleParser parser = new();
        parser.SetSpecialChars(SpecialChars);
        parser.SetCommentCallback(IsComment);
        parser.Parse(data, keepEmptyLines: true, parseQuotes: false);
        return parser;
    }

    private static bool IsUselessLine(string item)
    {
        if (item.EqualsIgnoreCase("Engine"))
            return true;
        if (item.EqualsIgnoreCase("IWAD"))
            return true;

        return false;
    }

    public bool GetEntityDefinitionName(int thingNumber, [NotNullWhen(true)] out string? name)
    {
        name = null;
        int index = thingNumber - 1;
        if (index < 0)
            return false;

        if (index < ActorNames.Length)
        {
            name = ActorNames[index];
            return true;
        }

        if (NewThingLookup.TryGetValue(index, out EntityDefinition? def))
        {
            name = def.Name;
            return true;
        }

        return false;
    }

    public bool GetEntityDefinition(int thingNumber, [NotNullWhen(true)] out EntityDefinition? def)
    {
        def = null;
        int index = thingNumber - 1;
        if (index < 0)
            return false;

        if (index < ActorDefinitions.Length)
        {
            def = ActorDefinitions[index];
            return def != null;
        }

        if (NewThingLookup.TryGetValue(index, out def))
            return true;

        return false;
    }

    public bool TryGetId24PickupType(EntityDefinitionComposer composer, int pickupItemType, [NotNullWhen(true)] out EntityDefinition? definition)
    {
        definition = null;
        if (pickupItemType < 0 || pickupItemType >= Id24PickupLookup.Length)
            return false;

        definition = composer.GetByName(Id24PickupLookup[pickupItemType]);
        if (definition == null)
            return false;

        return true;
    }

    public bool GetSoundName(int soundIndex, [NotNullWhen(true)] out string? soundName)
    {
        if (soundIndex >= 0 && soundIndex < SoundStrings.Length)
        {
            soundName = SoundStrings[soundIndex];
            return true;
        }

        if (NewSoundLookup.TryGetValue(soundIndex, out soundName))
            return true;
        
        return false;
    }

    private static bool IsComment(string line, int i) => i == 0 && line[i] == '#';

    private static void UnknownWarning(SimpleParser parser, string type, string? prefix = null)
    {
        int lineNumber = parser.GetCurrentLine();
        string line = parser.ConsumeLine();
        if (prefix != null)
            line = prefix + " " + line;
        if (string.IsNullOrWhiteSpace(line))
            return;
        Log.Warn($"Dehacked: Skipping unknown {type}: {line} line:{lineNumber}");
    }

    private SimpleParser ParseHeader(SimpleParser parser, string data)
    {
        DoomVersion = 0;
        PatchFormat = 0;
        while (!parser.IsDone() && (DoomVersion == 0 || PatchFormat == 0))
        {
            string item = parser.PeekLine();
            if (item.StartsWith('#'))
            {
                parser.ConsumeLine();
                continue;
            }

            if (item.StartsWith(DoomVersionName, StringComparison.OrdinalIgnoreCase))
                DoomVersion = GetIntProperty(parser, DoomVersionName);
            else if (item.StartsWith(PatchFormatName, StringComparison.OrdinalIgnoreCase))
                PatchFormat = GetIntProperty(parser, PatchFormatName);
            else
                parser.ConsumeLine();
        }

        // No header, reset to normal
        if (parser.IsDone())
            return CreateDehackedParser(data);

        return parser;
    }

    private void ParseThing(SimpleParser parser)
    {
        int lineNumber = parser.GetCurrentLine();
        DehackedThing thing = new();
        thing.Number = parser.ConsumeInteger();
        if (parser.Peek('('))
            thing.Name = parser.ConsumeLine();
        ConsumeLine(parser, lineNumber);

        while (!IsBlockComplete(parser))
        {
            lineNumber = parser.GetCurrentLine();
            string line = parser.PeekLine();
            if (line.StartsWithIgnoreCase(IDNumber))
                thing.ID = GetIntProperty(parser, IDNumber);
            else if (line.StartsWithIgnoreCase(InitFrame))
                thing.InitFrame = GetIntProperty(parser, InitFrame);
            else if (line.StartsWithIgnoreCase(Hitpoints))
                thing.Hitpoints = GetIntProperty(parser, Hitpoints);
            else if (line.StartsWithIgnoreCase(FirstMovingFrame))
                thing.FirstMovingFrame = GetIntProperty(parser, FirstMovingFrame);
            else if (line.StartsWithIgnoreCase(AlertSound))
                thing.AlertSound = GetIntProperty(parser, AlertSound);
            else if (line.StartsWithIgnoreCase(ReactionTime))
                thing.ReactionTime = GetIntProperty(parser, ReactionTime);
            else if (line.StartsWithIgnoreCase(AttackSound))
                thing.AttackSound = GetIntProperty(parser, AttackSound);
            else if (line.StartsWithIgnoreCase(InjuryFrame))
                thing.InjuryFrame = GetIntProperty(parser, InjuryFrame);
            else if (line.StartsWithIgnoreCase(PainChance))
                thing.PainChance = GetIntProperty(parser, PainChance);
            else if (line.StartsWithIgnoreCase(PainSound))
                thing.PainSound = GetIntProperty(parser, PainSound);
            else if (line.StartsWithIgnoreCase(CloseAttackFrame))
                thing.CloseAttackFrame = GetIntProperty(parser, CloseAttackFrame);
            else if (line.StartsWithIgnoreCase(FarAttackFrame))
                thing.FarAttackFrame = GetIntProperty(parser, FarAttackFrame);
            else if (line.StartsWithIgnoreCase(DeathFrame))
                thing.DeathFrame = GetIntProperty(parser, DeathFrame);
            else if (line.StartsWithIgnoreCase(ExplodingFrame))
                thing.ExplodingFrame = GetIntProperty(parser, ExplodingFrame);
            else if (line.StartsWithIgnoreCase(DeathSound))
                thing.DeathSound = GetIntProperty(parser, DeathSound);
            else if (line.StartsWithIgnoreCase(Speed))
                thing.Speed = GetIntProperty(parser, Speed);
            else if (line.StartsWithIgnoreCase(Width))
                thing.Width = GetIntProperty(parser, Width);
            else if (line.StartsWithIgnoreCase(Height))
                thing.Height = GetIntProperty(parser, Height);
            else if (line.StartsWithIgnoreCase(Mass))
                thing.Mass = GetIntProperty(parser, Mass);
            else if (line.StartsWithIgnoreCase(MisileDamage))
                thing.MisileDamage = GetIntProperty(parser, MisileDamage);
            else if (line.StartsWithIgnoreCase(ActionSound))
                thing.ActionSound = GetIntProperty(parser, ActionSound);
            else if (line.StartsWithIgnoreCase(RespawnFrame))
                thing.RespawnFrame = GetIntProperty(parser, RespawnFrame);
            else if (line.StartsWithIgnoreCase(DroppedItem))
                thing.DroppedItem = GetIntProperty(parser, DroppedItem);
            else if (line.StartsWithIgnoreCase(GibHealth))
                thing.GibHealth = GetIntProperty(parser, GibHealth);
            else if (line.StartsWithIgnoreCase(Bits))
                thing.Bits = GetBits(parser, Bits, ThingPropertyStrings);
            else if (line.StartsWithIgnoreCase(Mbf21Bits))
                thing.Mbf21Bits = GetBits(parser, Mbf21Bits, ThingPropertyStringsMbf21);
            else if (line.StartsWithIgnoreCase(InfightingGroup))
                thing.InfightingGroup = GetIntProperty(parser, InfightingGroup);
            else if (line.StartsWithIgnoreCase(ProjectileGroup))
                thing.ProjectileGroup = GetIntProperty(parser, ProjectileGroup);
            else if (line.StartsWithIgnoreCase(SplashGroup))
                thing.SplashGroup = GetIntProperty(parser, SplashGroup);
            else if (line.StartsWithIgnoreCase(RipSound))
                thing.RipSound = GetIntProperty(parser, RipSound);
            else if (line.StartsWithIgnoreCase(FastSpeed))
                thing.FastSpeed = GetIntProperty(parser, FastSpeed);
            else if (line.StartsWithIgnoreCase(MeleeRange))
                thing.MeleeRange = GetIntProperty(parser, MeleeRange);
            else if (line.StartsWithIgnoreCase(Id24Bits))
                thing.Id24Bits = GetBits(parser, Id24Bits, ThingPropertyStringsId24);
            else if (line.StartsWithIgnoreCase(MinRespawnTicks))
                thing.MinRespawnTicks = GetIntProperty(parser, MinRespawnTicks);
            else if (line.StartsWithIgnoreCase(RespawnDice))
                thing.RespawnDice = GetIntProperty(parser, RespawnDice);
            else if (line.StartsWithIgnoreCase(PickupAmmoType))
                thing.PickupAmmoType = GetIntProperty(parser, PickupAmmoType);
            else if (line.StartsWithIgnoreCase(PickupAmmoCategory))
                thing.PickupAmmoCategory = (Id24AmmoCategory)GetIntProperty(parser, PickupAmmoCategory);
            else if (line.StartsWithIgnoreCase(PickupWeaponType))
                thing.PickupWeaponType = GetIntProperty(parser, PickupWeaponType);
            else if (line.StartsWithIgnoreCase(PickupItemType))
                thing.PickupItemType = (Id24PickupType?)GetIntProperty(parser, PickupItemType);
            else if (line.StartsWithIgnoreCase(PickupBonusCount))
                thing.PickupBonusCount = GetIntProperty(parser, PickupBonusCount);
            else if (line.StartsWithIgnoreCase(PickupSound))
                thing.PickupSound = GetIntProperty(parser, PickupSound);
            else if (line.StartsWithIgnoreCase(PickupMessage))
                thing.PickupMessage = GetStringProperty(parser, PickupMessage);
            else if (line.StartsWithIgnoreCase(TranslationLump))
                thing.TranslationLump = GetStringProperty(parser, TranslationLump);
            else if (line.StartsWithIgnoreCase(SelfDamageFactor))
                thing.SelfDamageFactor = MathHelper.FromFixed(GetIntProperty(parser, SelfDamageFactor));
            else if (!IgnoreLine(line))
                UnknownWarning(parser, "thing type");

            ConsumeLine(parser, lineNumber);
        }

        Things.Add(thing);
    }

    private static bool IgnoreLine(string line) =>
        line.StartsWithIgnoreCase(Plural) || line.StartsWithIgnoreCase(Name1) || line.StartsWithIgnoreCase(RetroBits);

    private void ParseFrame(SimpleParser parser)
    {
        int lineNumber = parser.GetCurrentLine();
        DehackedFrame frame = new();
        frame.Frame = parser.ConsumeInteger();

        // Sometimes there is text after the frame. eg. Frame 10 (description)
        ConsumeLine(parser, lineNumber);

        while (!IsBlockComplete(parser))
        {
            lineNumber = parser.GetCurrentLine();
            string line = parser.PeekLine();
            if (line.StartsWith(SpriteNum, StringComparison.OrdinalIgnoreCase))
                frame.SpriteNumber = GetIntProperty(parser, SpriteNum);
            else if (line.StartsWith(SpriteSubNum, StringComparison.OrdinalIgnoreCase))
                frame.SpriteSubNumber = GetIntProperty(parser, SpriteSubNum);
            else if (line.StartsWith(Duration, StringComparison.OrdinalIgnoreCase))
                frame.Duration = GetIntProperty(parser, Duration);
            else if (line.StartsWith(NextFrame, StringComparison.OrdinalIgnoreCase))
                frame.NextFrame = GetIntProperty(parser, NextFrame);
            else if (line.StartsWith(Unknown1, StringComparison.OrdinalIgnoreCase))
                frame.Unknown1 = GetIntProperty(parser, Unknown1);
            else if (line.StartsWith(Unknown2, StringComparison.OrdinalIgnoreCase))
                frame.Unknown2 = GetIntProperty(parser, Unknown2);
            else if (line.StartsWith(Mbf21Bits, StringComparison.OrdinalIgnoreCase))
                frame.Mbf21Bits = GetBits(parser, Mbf21Bits, FramePropertyStringsMbf21);
            else if (IsArgs(line))
                SetFrameArgs(parser, line, frame);
            else
                UnknownWarning(parser, "frame type");

            ConsumeLine(parser, lineNumber);
        }

        Frames.Add(frame);
    }

    private static void SetFrameArgs(SimpleParser parser, string line, DehackedFrame frame)
    {
        const string FrameArgWarning = "Dehacked: Bad frame arg: ";
        if (line.Length < 5)
            return;

        if (!int.TryParse(line.AsSpan(4, 1), out int index))
        {
            Log.Warn($"{FrameArgWarning}{line}");
            return;
        }

        if (index < 1 || index > 8)
        {
            Log.Warn($"Dehacked: Bad frame arg: {line}");
            return;
        }

        parser.ConsumeString();
        parser.Consume('=');
        int value = ConsumeDehackedInteger(parser);

        switch (index)
        {
            case 1:
                frame.Args1 = value;
                break;
            case 2:
                frame.Args2 = value;
                break;
            case 3:
                frame.Args3 = value;
                break;
            case 4:
                frame.Args4 = value;
                break;
            case 5:
                frame.Args5 = value;
                break;
            case 6:
                frame.Args6 = value;
                break;
            case 7:
                frame.Args7 = value;
                break;
            case 8:
                frame.Args8 = value;
                break;
            default:
                break;
        }
    }

    private static bool IsArgs(string line)
    {
        if (line.Length < 5 || !line.StartsWith("ARGS", StringComparison.OrdinalIgnoreCase))
            return false;

        return char.IsDigit(line[4]);
    }

    private void ParseAmmo(SimpleParser parser)
    {
        int lineNumber = parser.GetCurrentLine();
        DehackedAmmo ammo = new();
        ammo.AmmoNumber = parser.ConsumeInteger();
        ConsumeLine(parser, lineNumber);

        while (!IsBlockComplete(parser))
        {
            lineNumber = parser.GetCurrentLine();
            string line = parser.PeekLine();
            if (line.StartsWith(MaxAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.MaxAmmo = GetIntProperty(parser, MaxAmmo);
            else if (line.StartsWith(PerAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.PerAmmo = GetIntProperty(parser, PerAmmo);
            else if (line.StartsWith(InitialAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.InitialAmmo = GetIntProperty(parser, InitialAmmo);
            else if (line.StartsWith(MaxUpgradedAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.MaxUpgradedAmmo = GetIntProperty(parser, MaxUpgradedAmmo);
            else if (line.StartsWith(BoxAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.BoxAmmo = GetIntProperty(parser, BoxAmmo);
            else if (line.StartsWith(BackpackAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.BackpackAmmo = GetIntProperty(parser, BackpackAmmo);
            else if (line.StartsWith(WeaponAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.WeaponAmmo = GetIntProperty(parser, WeaponAmmo);
            else if (line.StartsWith(DroppedAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.DroppedAmmo = GetIntProperty(parser, DroppedAmmo);
            else if (line.StartsWith(DroppedBoxAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.DroppedBoxAmmo = GetIntProperty(parser, DroppedBoxAmmo);
            else if (line.StartsWith(DroppedBoxAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.DroppedBoxAmmo = GetIntProperty(parser, DroppedBoxAmmo);
            else if (line.StartsWith(DroppedBackpackAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.DroppedBackpackAmmo = GetIntProperty(parser, DroppedBackpackAmmo);
            else if (line.StartsWith(DroppedWeaponAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.DroppedWeaponAmmo = GetIntProperty(parser, DroppedWeaponAmmo);
            else if (line.StartsWith(DeathmatchWeaponAmmo, StringComparison.OrdinalIgnoreCase))
                ammo.DeathmatchWeaponAmmo = GetIntProperty(parser, DeathmatchWeaponAmmo);
            else if (line.StartsWith(Skill1Multiplier, StringComparison.OrdinalIgnoreCase))
                ammo.Skill1Multiplier = MathHelper.FromFixed(GetIntProperty(parser, Skill1Multiplier));
            else if (line.StartsWith(Skill2Multiplier, StringComparison.OrdinalIgnoreCase))
                ammo.Skill2Multiplier = MathHelper.FromFixed(GetIntProperty(parser, Skill2Multiplier));
            else if (line.StartsWith(Skill3Multiplier, StringComparison.OrdinalIgnoreCase))
                ammo.Skill3Multiplier = MathHelper.FromFixed(GetIntProperty(parser, Skill3Multiplier));
            else if (line.StartsWith(Skill4Multiplier, StringComparison.OrdinalIgnoreCase))
                ammo.Skill4Multiplier = MathHelper.FromFixed(GetIntProperty(parser, Skill4Multiplier));
            else if (line.StartsWith(Skill5Multiplier, StringComparison.OrdinalIgnoreCase))
                ammo.Skill5Multiplier = MathHelper.FromFixed(GetIntProperty(parser, Skill5Multiplier));
            else
                UnknownWarning(parser, "ammo type");
            ConsumeLine(parser, lineNumber);
        }

        Ammo.Add(ammo);
    }

    private void ParseWeapon(SimpleParser parser)
    {
        DehackedWeapon weapon = new();
        int lineNumber = parser.GetCurrentLine();
        weapon.WeaponNumber = parser.ConsumeInteger();
        ConsumeLine(parser, lineNumber);

        while (!IsBlockComplete(parser))
        {
            lineNumber = parser.GetCurrentLine();
            string line = parser.PeekLine();
            if (line.StartsWith(DeselectFrame, StringComparison.OrdinalIgnoreCase))
                weapon.DeselectFrame = GetIntProperty(parser, DeselectFrame);
            else if (line.StartsWith(SelectFrame, StringComparison.OrdinalIgnoreCase))
                weapon.SelectFrame = GetIntProperty(parser, SelectFrame);
            else if (line.StartsWith(AmmoType, StringComparison.OrdinalIgnoreCase))
                weapon.AmmoType = GetIntProperty(parser, AmmoType);
            else if (line.StartsWith(BobbingFrame, StringComparison.OrdinalIgnoreCase))
                weapon.BobbingFrame = GetIntProperty(parser, BobbingFrame);
            else if (line.StartsWith(ShootingFrame, StringComparison.OrdinalIgnoreCase))
                weapon.ShootingFrame = GetIntProperty(parser, ShootingFrame);
            else if (line.StartsWith(FiringFrame, StringComparison.OrdinalIgnoreCase))
                weapon.FiringFrame = GetIntProperty(parser, FiringFrame);
            else if (line.StartsWith(AmmoPerShot, StringComparison.OrdinalIgnoreCase))
                weapon.AmmoPerShot = GetIntProperty(parser, AmmoPerShot);
            else if (line.StartsWith(AmmoUse, StringComparison.OrdinalIgnoreCase))
                weapon.AmmoPerShot = GetIntProperty(parser, AmmoUse);
            else if (line.StartsWith(MinAmmo, StringComparison.OrdinalIgnoreCase))
                weapon.MinAmmo = GetIntProperty(parser, MinAmmo);
            else if (line.StartsWith(Mbf21Bits, StringComparison.OrdinalIgnoreCase))
                weapon.Mbf21Bits = GetBits(parser, Mbf21Bits, WeaponPropertyStringsMbf21);
            else if (line.StartsWith(WeaponSlotPriority, StringComparison.OrdinalIgnoreCase))
                weapon.SlotPriority = GetIntProperty(parser, WeaponSlotPriority);
            else if (line.StartsWith(WeaponSlot, StringComparison.OrdinalIgnoreCase))
                weapon.Slot = GetIntProperty(parser, WeaponSlot);
            else if (line.StartsWith(WeaponSwitchPriority, StringComparison.OrdinalIgnoreCase))
                weapon.SwitchPriority = GetIntProperty(parser, WeaponSwitchPriority);
            else if (line.StartsWith(InitialOwned, StringComparison.OrdinalIgnoreCase))
                weapon.InitialOwned = GetIntProperty(parser, InitialOwned) != 0;
            else if (line.StartsWith(InitialRaised, StringComparison.OrdinalIgnoreCase))
                weapon.InitialRaised = GetIntProperty(parser, InitialRaised) != 0;
            else if (line.StartsWith(CarouselIcon, StringComparison.OrdinalIgnoreCase))
                weapon.CarouselIcon = GetStringProperty(parser, CarouselIcon);
            else if (line.StartsWith(AllowSwitchWithOwnedWeapon, StringComparison.OrdinalIgnoreCase))
                weapon.AllowSwitchWithOwnedWeapon = GetIntProperty(parser, AllowSwitchWithOwnedWeapon);
            else if (line.StartsWith(NoSwitchWithOwnedWeapon, StringComparison.OrdinalIgnoreCase))
                weapon.NoSwitchWithOwnedWeapon = GetIntProperty(parser, NoSwitchWithOwnedWeapon);
            else if (line.StartsWith(AllowSwitchWithOwnedItem, StringComparison.OrdinalIgnoreCase))
                weapon.AllowSwitchWithOwnedItem = GetIntProperty(parser, AllowSwitchWithOwnedItem);
            else if (line.StartsWith(NoSwitchWithOwnedItem, StringComparison.OrdinalIgnoreCase))
                weapon.NoSwitchWithOwnedItem = GetIntProperty(parser, NoSwitchWithOwnedItem);
            else
                UnknownWarning(parser, "weapon type");

            ConsumeLine(parser, lineNumber);
        }

        Weapons.Add(weapon);
    }

    private void ParseCheat(SimpleParser parser)
    {
        int lineNumber = parser.GetCurrentLine();
        Cheat = new();
        ConsumeLine(parser, lineNumber);

        while (!IsBlockComplete(parser))
        {
            lineNumber = parser.GetCurrentLine();
            string line = parser.PeekLine();
            if (line.StartsWith(ChangeMusic, StringComparison.OrdinalIgnoreCase))
                Cheat.ChangeMusic = GetStringProperty(parser, ChangeMusic);
            else if (line.StartsWith(Chainsaw, StringComparison.OrdinalIgnoreCase))
                Cheat.Chainsaw = GetStringProperty(parser, Chainsaw);
            else if (line.StartsWith(God, StringComparison.OrdinalIgnoreCase))
                Cheat.God = GetStringProperty(parser, God);
            else if (line.StartsWith(AmmoAndKeys, StringComparison.OrdinalIgnoreCase))
                Cheat.AmmoAndKeys = GetStringProperty(parser, AmmoAndKeys);
            else if (line.StartsWith(AmmoCheat, StringComparison.OrdinalIgnoreCase))
                Cheat.Ammo = GetStringProperty(parser, AmmoCheat);
            else if (line.StartsWith(NoClip1, StringComparison.OrdinalIgnoreCase))
                Cheat.NoClip1 = GetStringProperty(parser, NoClip1);
            else if (line.StartsWith(NoClip2, StringComparison.OrdinalIgnoreCase))
                Cheat.NoClip2 = GetStringProperty(parser, NoClip2);
            else if (line.StartsWith(Invincibility, StringComparison.OrdinalIgnoreCase))
                Cheat.Invincibility = GetStringProperty(parser, Invincibility);
            else if (line.StartsWith(Invisibility, StringComparison.OrdinalIgnoreCase))
                Cheat.Invisibility = GetStringProperty(parser, Invisibility);
            else if (line.StartsWith(RadSuit, StringComparison.OrdinalIgnoreCase))
                Cheat.RadSuit = GetStringProperty(parser, RadSuit);
            else if (line.StartsWith(AutoMap, StringComparison.OrdinalIgnoreCase))
                Cheat.AutoMap = GetStringProperty(parser, AutoMap);
            else if (line.StartsWith(LiteAmp, StringComparison.OrdinalIgnoreCase))
                Cheat.LiteAmp = GetStringProperty(parser, LiteAmp);
            else if (line.StartsWith(Behold, StringComparison.OrdinalIgnoreCase))
                Cheat.Behold = GetStringProperty(parser, Behold);
            else if (line.StartsWith(LevelWarp, StringComparison.OrdinalIgnoreCase))
                Cheat.LevelWarp = GetStringProperty(parser, LevelWarp);
            else if (line.StartsWith(MapCheat, StringComparison.OrdinalIgnoreCase))
                Cheat.LevelWarp = GetStringProperty(parser, MapCheat);
            else if (line.StartsWith(PlayerPos, StringComparison.OrdinalIgnoreCase))
                Cheat.PlayerPos = GetStringProperty(parser, PlayerPos);
            else if (line.StartsWith(Berserk, StringComparison.OrdinalIgnoreCase))
                Cheat.Berserk = GetStringProperty(parser, Berserk);
            else
                UnknownWarning(parser, "cheat type");

            ConsumeLine(parser, lineNumber);
        }
    }

    private void ParseText(SimpleParser parser)
    {
        DehackedString text = new();
        text.OldSize = parser.ConsumeInteger();
        text.NewSize = parser.ConsumeInteger();
        m_sb.Clear();

        while (!IsBlockComplete(parser))
        {
            m_sb.Append(parser.ConsumeLine(keepBeginningSpaces: true));
            m_sb.Append('\n');
            // Empty strings get eaten by IsBlockComplete
            if (string.IsNullOrEmpty(parser.PeekString()))
                m_sb.Append('\n');
        }

        while (m_sb.Length > 0 && m_sb[m_sb.Length - 1] == '\n')
            m_sb.Length--;

        if (text.OldSize > m_sb.Length)
        {
            Log.Warn($"Dehacked: Invalid dehacked string length:{text.OldSize} line:{parser.GetCurrentLine()}");
            return;
        }

        string sbText = m_sb.ToString();
        text.OldString = sbText.Substring(0, text.OldSize);
        text.NewString = sbText.Substring(text.OldSize);

        Strings.Add(text);
    }

    private void ParsePointer(SimpleParser parser)
    {
        DehackedPointer pointer = new();
        pointer.Number = parser.ConsumeInteger();

        var offset = parser.GetCurrentOffset();
        string text = parser.ConsumeLine();
        var match = PointerRegex.Match(text);
        if (!match.Success || match.Groups.Count < 2)
            throw new ParserException(offset.Line, offset.Char, -1, $"Invalid pointer text: {text}");

        string frame = match.Groups[1].Value;
        if (!int.TryParse(frame, out int frameNumber))
        {
            Log.Warn($"Dehacked: Invalid frame:{frame} line:{parser.GetCurrentLine()}");
            return;
        }

        pointer.Frame = frameNumber;

        while (!IsBlockComplete(parser))
        {
            string line = parser.PeekLine();
            if (line.StartsWith("Codep Frame", StringComparison.OrdinalIgnoreCase))
                pointer.CodePointerFrame = GetIntProperty(parser, DeselectFrame);
            else
                UnknownWarning(parser, "pointer type");

            if (!parser.IsDone())
                parser.ConsumeLine();
        }

        Pointers.Add(pointer);
    }

    private void ParseMisc(SimpleParser parser, int itemLine)
    {
        Misc = new();

        // Can have number in brackets (e.g. [0]) just eat it
        if (parser.GetCurrentLine() == itemLine)
            parser.ConsumeLine();

        while (!IsBlockComplete(parser))
        {
            int lineNumber = parser.GetCurrentLine();
            string item = parser.PeekLine();

            if (item.StartsWith(InitialHealth, StringComparison.OrdinalIgnoreCase))
                Misc.InitialHealth = GetIntProperty(parser, InitialHealth);
            else if (item.StartsWith(InitialBullets, StringComparison.OrdinalIgnoreCase))
                Misc.InitialBullets = GetIntProperty(parser, InitialBullets);
            else if (item.StartsWith(MaxHealth, StringComparison.OrdinalIgnoreCase))
                Misc.MaxHealth = GetIntProperty(parser, MaxHealth);
            else if (item.StartsWith(MaxArmor, StringComparison.OrdinalIgnoreCase))
                Misc.MaxArmor = GetIntProperty(parser, MaxArmor);
            else if (item.StartsWith(GreenArmorClass, StringComparison.OrdinalIgnoreCase))
                Misc.GreenArmorClass = GetIntProperty(parser, GreenArmorClass);
            else if (item.StartsWith(BlueArmorClass, StringComparison.OrdinalIgnoreCase))
                Misc.BlueArmorClass = GetIntProperty(parser, BlueArmorClass);
            else if (item.StartsWith(MaxSoulsphere, StringComparison.OrdinalIgnoreCase))
                Misc.MaxSoulsphere = GetIntProperty(parser, MaxSoulsphere);
            else if (item.StartsWith(SoulsphereHealth, StringComparison.OrdinalIgnoreCase))
                Misc.SoulsphereHealth = GetIntProperty(parser, SoulsphereHealth);
            else if (item.StartsWith(MegasphereHealth, StringComparison.OrdinalIgnoreCase))
                Misc.MegasphereHealth = GetIntProperty(parser, MegasphereHealth);
            else if (item.StartsWith(GodModeHealth, StringComparison.OrdinalIgnoreCase))
                Misc.GodModeHealth = GetIntProperty(parser, GodModeHealth);
            else if (item.StartsWith(IDFAArmorClass, StringComparison.OrdinalIgnoreCase))
                Misc.IdfaArmorClass = GetIntProperty(parser, IDFAArmorClass);
            else if (item.StartsWith(IDFAArmor, StringComparison.OrdinalIgnoreCase))
                Misc.IdfaArmor = GetIntProperty(parser, IDFAArmor);
            else if (item.StartsWith(IDKFAArmorClass, StringComparison.OrdinalIgnoreCase))
                Misc.IdkfaArmorClass = GetIntProperty(parser, IDKFAArmorClass);
            else if (item.StartsWith(IDKFAArmor, StringComparison.OrdinalIgnoreCase))
                Misc.IdkfaArmor = GetIntProperty(parser, IDKFAArmor);
            else if (item.StartsWith(BFGCellsPerShot, StringComparison.OrdinalIgnoreCase))
                Misc.BfgCellsPerShot = GetIntProperty(parser, BFGCellsPerShot);
            else if (item.StartsWith(MonstersInfight, StringComparison.OrdinalIgnoreCase))
                Misc.MonstersInfight = (MonsterInfightType)GetIntProperty(parser, MonstersInfight);
            else if (item.StartsWith(MonstersIgnore, StringComparison.OrdinalIgnoreCase))
                Misc.MonstersIgnoreEachOther = GetIntProperty(parser, MonstersIgnore) != 0;
            else
                UnknownWarning(parser, "misc");

            ConsumeLine(parser, lineNumber);
        }
    }

    private void ParseSound(SimpleParser parser)
    {
        DehackedSound sound = new();
        sound.Number = parser.ConsumeInteger();

        while (!IsBlockComplete(parser))
        {
            int lineNumber = parser.GetCurrentLine();
            string line = parser.PeekLine();
            if (IgnoreSoundProperties.Any(x => line.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                parser.ConsumeLine();
                continue;
            }

            if (line.StartsWith(SoundZeroOne, StringComparison.OrdinalIgnoreCase))
                sound.ZeroOne = GetIntProperty(parser, SoundZeroOne);
            else if (line.StartsWith(SoundValue, StringComparison.OrdinalIgnoreCase))
                sound.Priority = GetIntProperty(parser, SoundValue);
            else
                UnknownWarning(parser, "sound");
            ConsumeLine(parser, lineNumber);
        }

        Sounds.Add(sound);
    }

    private void ParseBexString(SimpleParser parser)
    {
        parser.ConsumeString();

        while (!IsBlockComplete(parser, isBex: true))
        {
            BexString bexString = new();
            bexString.Mnemonic = parser.ConsumeString();
            parser.ConsumeString("=");
            bexString.Value = ConsumeBexTextValue(parser);
            BexStrings.Add(bexString);
        }
    }

    private string ConsumeBexTextValue(SimpleParser parser)
    {
        m_sb.Clear();
        while (true)
        {
            var value = parser.ConsumeLine().Replace("\\n", "\n");
            if (!value.EndsWith('\\'))
            {
                m_sb.Append(value);
                return m_sb.ToString();
            }
            m_sb.Append(value.AsSpan(0, value.Length - 1));
        }
    }

    private void ParseBexPointer(SimpleParser parser)
    {
        parser.ConsumeString();

        while (!IsBexPointerBlockComplete(parser))
        {
            int lineNumber = parser.GetCurrentLine();
            parser.ConsumeString("Frame");
            int frame = parser.ConsumeInteger();
            parser.ConsumeString("=");
            string name = parser.ConsumeString();
            Pointers.Add(new DehackedPointer() { Frame = frame, CodePointerMnemonic = name });
            ConsumeLine(parser, lineNumber);
        }
    }

    private void ParseBexPar(SimpleParser parser)
    {
        parser.ConsumeString();

        while (!IsBlockComplete(parser, isBex: true))
        {
            int lineNumber = parser.GetCurrentLine();
            parser.ConsumeString("par");
            int item1 = parser.ConsumeInteger();
            int item2 = parser.ConsumeInteger();
            int? item3 = null;

            if (parser.GetCurrentLine() == lineNumber && parser.PeekInteger(out int peekInt))
                item3 = peekInt;

            if (item3.HasValue)
                BexPars.Add(new BexPar() { Episode = item1, Map = item2, Par = item3.Value });
            else
                BexPars.Add(new BexPar() { Map = item1, Par = item2 });
            ConsumeLine(parser, lineNumber);
        }
    }

    private void ParseBexItem(SimpleParser parser, List<BexItem> items)
    {
        parser.ConsumeString();

        while (!IsBlockComplete(parser, isBex: true))
        {
            int lineNumber = parser.GetCurrentLine();
            string? mnemonic = null;
            int? index = parser.ConsumeIfInt();
            if (index == null)
                mnemonic = parser.ConsumeString();

            parser.ConsumeIf("=");

            string entry = parser.ConsumeString();
            items.Add(new BexItem() { Mnemonic = mnemonic, Index = index, EntryName = entry });
            ConsumeLine(parser, lineNumber);
        }
    }

    private static void ConsumeLine(SimpleParser parser, int lineNumber)
    {
        if (parser.GetCurrentLine() == lineNumber)
            parser.ConsumeLine();
    }

    private static bool IsBexPointerBlockComplete(SimpleParser parser)
    {
        if (parser.PeekString(0, out string? frame) && parser.PeekString(2, out string? equal)
            && frame != null && equal != null)
        {
            return !frame.Equals("Frame", StringComparison.OrdinalIgnoreCase) || !equal.Equals("=");
        }

        return true;
    }

    private bool IsBlockComplete(SimpleParser parser, bool isBex = false)
    {
        if (parser.IsDone())
            return true;

        string peek = parser.PeekString();
        while (string.IsNullOrEmpty(peek))
        {
            parser.ConsumeString();
            if (parser.IsDone())
                return true;

            peek = parser.PeekString();
        }

        if (BexBaseTypes.Contains(peek))
            return true;

        // Dehacked base types are all proceeded by a number, check to not confuse with random text
        if (BaseTypes.Contains(peek) && parser.PeekString(1, out string? data) &&
            int.TryParse(data, out _))
            return true;

        return false;
    }

    private static uint GetBits(SimpleParser parser, string property, IReadOnlyDictionary<string, uint> lookup)
    {
        ConsumeProperty(parser, property);
        parser.ConsumeString("=");
        uint? bits = (uint?)parser.ConsumeIfInt();
        if (bits.HasValue)
            return bits.Value;

        return ParseStringBits(parser, lookup);
    }

    private static readonly string[] StringBitsSplit = ["+", "|", ","];

    private static uint ParseStringBits(SimpleParser parser, IReadOnlyDictionary<string, uint> lookup)
    {
        uint bits = 0;
        string[] items = parser.ConsumeLine().Split(StringBitsSplit, StringSplitOptions.RemoveEmptyEntries);

        foreach (string item in items)
        {
            string stringFlag = item.Trim();
            if (lookup.TryGetValue(stringFlag, out uint flag))
                bits |= flag;
            else
                Log.Warn($"Dehacked: Invalid thing flag {stringFlag}.");
        }

        return bits;
    }

    private static string GetStringProperty(SimpleParser parser, string property)
    {
        ConsumeProperty(parser, property);
        parser.ConsumeString("=");
        return parser.ConsumeString();
    }

    private static int GetIntProperty(SimpleParser parser, string property)
    {
        ConsumeProperty(parser, property);
        parser.ConsumeString("=");
        int? value = parser.ConsumeIfInt();
        if (value != null)
            return value.Value;

        // Dehacked parsers used sscanf which would read until a non digit was hit.
        // Consume int expects the entire token to be an integer.
        return ConsumeDehackedInteger(parser);
    }

    private static int ConsumeDehackedInteger(SimpleParser parser)
    {
        string data = parser.ConsumeString();
        int end = 0;
        if (data[0] == '-')
            end++;
        while (end < data.Length && char.IsDigit(data[end]))
            end++;

        if (!int.TryParse(data.AsSpan(0, end), out int i))
            throw new Exception($"Expected an integer but got {data}");
        return i;
    }

    private static void ConsumeProperty(SimpleParser parser, string property)
    {
        for (int i = 0; i < property.Count(x => x == ' ') + 1; i++)
            parser.ConsumeString();
    }
}
