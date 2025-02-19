using Helion.Audio;
using Helion.Util.Configs.Impl;
using Helion.Util.Configs.Options;
using Helion.Util.Configs.Values;
using System.IO;
using static Helion.Util.Configs.Values.ConfigFilters;

namespace Helion.Util.Configs.Components;

public class ConfigAudio: ConfigElement<ConfigAudio>
{
    [ConfigInfo("Music volume. 0.0 is Off, 2.0 is Maximum.")]
    [OptionMenu(OptionSectionType.Audio, "Music Volume", sliderMin: 0, sliderMax: 2.0, sliderStep: .05)]
    public readonly ConfigValue<double> MusicVolume = new(1.0, Clamp(0, 2.0));

    [ConfigInfo("Sound effect volume. 0.0 is Off, 2.0 is Maximum.")]
    [OptionMenu(OptionSectionType.Audio, "Sound Volume", sliderMin: 0, sliderMax: 2.0, sliderStep: .05)]
    public readonly ConfigValue<double> SoundVolume = new(1.0, Clamp(0, 2.0));

    [ConfigInfo("Enables sound velocity.")]
    [OptionMenu(OptionSectionType.Audio, "Sound Velocity", spacer: true)]
    public readonly ConfigValue<bool> Velocity = new(false);

    [ConfigInfo("Randomize sound pitch.")]
    [OptionMenu(OptionSectionType.Audio, "Randomize Pitch", spacer: true)]
    public readonly ConfigValue<RandomPitch> RandomizePitch = new(RandomPitch.None);

    [ConfigInfo("Randomized pitch scale value.")]
    [OptionMenu(OptionSectionType.Audio, "Random Pitch Scale", sliderMin: 0.1, sliderMax: 10, sliderStep: .1)]
    public readonly ConfigValue<double> RandomPitchScale = new(1, Clamp(0.1, 10));

    [ConfigInfo("Scale for sound pitch.")]
    [OptionMenu(OptionSectionType.Audio, "Pitch Scale", sliderMin: 0.1, sliderMax: 10, sliderStep: .1)]
    public readonly ConfigValue<double> Pitch = new(1, Clamp(0.1, 10));

    [ConfigInfo("Log sound errors.")]
    [OptionMenu(OptionSectionType.Audio, "Log Sound Errors", spacer: true)]
    public readonly ConfigValue<bool> LogErrors = new(false);

    [ConfigInfo("Main device to use for audio.")]
    public readonly ConfigValue<string> Device = new(IAudioSystem.DefaultAudioDevice);

    [ConfigInfo("Synthesizer to use for music.")]
    [OptionMenu(OptionSectionType.Audio, "Music Synthesizer")]
    public readonly ConfigValue<Synth> Synthesizer = new(Synth.FluidSynth);

    [ConfigInfo("SoundFont file to use for MIDI/MUS music playback (FluidSynth only).")]
    [OptionMenu(OptionSectionType.Audio, "SoundFont File", dialogType: DialogType.SoundFontPicker)]
    public readonly ConfigValue<string> SoundFontFile = new($"SoundFonts{Path.DirectorySeparatorChar}Default.sf2");

    [ConfigInfo("Enable chorus effect in MIDI/MUS playback (FluidSynth only).")]
    [OptionMenu(OptionSectionType.Audio, "Enable Chorus")]
    public readonly ConfigValue<bool> EnableChorus = new(true);

    [ConfigInfo("Enable reverb effect in MIDI/MUS playback (FluidSynth only).")]
    [OptionMenu(OptionSectionType.Audio, "Enable Reverb")]
    public readonly ConfigValue<bool> EnableReverb = new(true);

    // Music volume is treated as a multiple of sound effects volume, because effects volume controls the master gain.
    public double MusicVolumeNormalized => SoundVolume == 0 ? MusicVolume : (MusicVolume / SoundVolume);
}
