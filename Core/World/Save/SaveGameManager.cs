using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Helion.Graphics;
using Helion.Models;
using Helion.Resources.Archives.Collection;
using Helion.Util;
using Helion.Util.Configs;
using Helion.Util.Extensions;
using Helion.World.Util;
using NLog;

namespace Helion.World.Save;

public readonly struct SaveGameEvent(SaveGame saveGame, WorldModel worldModel, string filename, bool success, Exception? ex = null)
{
    public readonly SaveGame SaveGame = saveGame;
    public readonly WorldModel WorldModel = worldModel;
    public readonly string FileName = filename;
    public readonly bool Success = success;
    public readonly Exception? Exception = ex;
}

public class SaveGameManager(IConfig config, ArchiveCollection archiveCollection, string? saveDirCommandLineArg)
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly IConfig m_config = config;
    private readonly ArchiveCollection m_archiveCollection = archiveCollection;
    private readonly string? m_saveDirCommandLineArg = saveDirCommandLineArg;
    private readonly List<SaveGame> m_currentSaves = [];
    private bool m_currentSavesLoaded;

    public event EventHandler<SaveGameEvent>? GameSaved;

    private string GetSaveDir()
    {
        if (string.IsNullOrEmpty(m_saveDirCommandLineArg))
            return Directory.GetCurrentDirectory();

        if (!EnsureDirectoryExists(m_saveDirCommandLineArg))
            return Directory.GetCurrentDirectory();

        return m_saveDirCommandLineArg;
    }

    private static bool EnsureDirectoryExists(string path)
    {
        if (Directory.Exists(path))
            return true;

        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch
        {
            Log.Error("Failed to create directory {dir}", path);
            return false;
        }
    }

    public void LoadCurrentSaveFiles()
    {
        if (m_currentSavesLoaded)
            return;

        var saveGames = GetMatchingSaveGames(ReadSaveGameFiles());
        m_currentSaves.AddRange(saveGames);
        m_currentSavesLoaded = true;
    }

    public bool SaveFileExists(string filename)
    {
        string filePath = Path.Combine(GetSaveDir(), filename);
        return File.Exists(filePath);
    }

    public SaveGame ReadSaveGame(string filename) => new(GetSaveDir(), filename);

    public Task<SaveGameEvent> WriteNewSaveGameAsync(IWorld world, string title, IScreenshotGenerator screenshotGenerator, bool autoSave = false, bool quickSave = false) =>
        WriteSaveGameAsync(world, title, screenshotGenerator, null, autoSave, quickSave);

    public async Task<SaveGameEvent> WriteSaveGameAsync(IWorld world, string title, IScreenshotGenerator screenshotGenerator, SaveGame? existingSave, bool autoSave = false, bool quickSave = false)
    {
        existingSave = GetExistingSave(existingSave, autoSave, quickSave);
        bool isNew = existingSave == null;
        var filename = existingSave?.FileName ?? GetNewSaveName(autoSave, quickSave);
        var worldModel = world.ToWorldModel();
        var image = screenshotGenerator.GetImage();
        var saveEvent = await Task.Run(() => SaveGame.WriteSaveGame(world, worldModel, title, GetSaveDir(), filename, screenshotGenerator, image));

        AddOrUpdateSaveGame(saveEvent.SaveGame);
        GameSaved?.Invoke(this, saveEvent);
        return saveEvent;
    }

    public SaveGameEvent WriteNewSaveGame(IWorld world, string title, IScreenshotGenerator screenshotGenerator, bool autoSave = false, bool quickSave = false) =>
        WriteSaveGame(world, title, screenshotGenerator, null, autoSave, quickSave);

    public SaveGameEvent WriteSaveGame(IWorld world, string title, IScreenshotGenerator screenshotGenerator, SaveGame? existingSave, bool autoSave = false, bool quickSave = false)
    {
        existingSave = GetExistingSave(existingSave, autoSave, quickSave);
        bool isNew = existingSave == null;
        var filename = existingSave?.FileName ?? GetNewSaveName(autoSave, quickSave);
        var worldModel = world.ToWorldModel();
        var saveEvent = SaveGame.WriteSaveGame(world, worldModel, title, GetSaveDir(), filename, screenshotGenerator, screenshotGenerator.GetImage());

        AddOrUpdateSaveGame(saveEvent.SaveGame);
        GameSaved?.Invoke(this, saveEvent);
        return saveEvent;
    }

    private void AddOrUpdateSaveGame(SaveGame newSaveGame)
    {
        for (int i = 0; i < m_currentSaves.Count; i++)
        {
            var save = m_currentSaves[i];
            if (save.FileName == newSaveGame.FileName)
            {
                m_currentSaves[i] = newSaveGame;
                return;
            }
        }

        m_currentSaves.Add(newSaveGame);
    }

    private SaveGame? GetExistingSave(SaveGame? existingSave, bool autoSave, bool quickSave)
    {
        var saveGames = GetSaveGames();
        if (existingSave == null && autoSave && m_config.Game.RotatingAutoSaves > 0)
        {
            var autoSaves = saveGames.Where(x => x.IsAutoSave).OrderBy(x => x.Model?.Date);
            if (autoSaves.Any() && autoSaves.Count() >= m_config.Game.RotatingAutoSaves)
                existingSave = autoSaves.First();
        }

        if (existingSave == null && quickSave && m_config.Game.RotatingQuickSaves > 0)
        {
            var quickSaves = saveGames.Where(x => x.IsQuickSave).OrderBy(x => x.Model?.Date);
            if (quickSaves.Any() && quickSaves.Count() >= m_config.Game.RotatingQuickSaves)
                existingSave = quickSaves.First();
        }

        return existingSave;
    }

    public IEnumerable<SaveGame> GetMatchingSaveGames(IEnumerable<SaveGame> saveGames)
    {
        return saveGames.Where(x => x.Model != null &&
            ModelVerification.VerifyModelFiles(x.Model.Files, m_archiveCollection, null));
    }

    public List<SaveGame> GetSaveGames()
    {
        LoadCurrentSaveFiles();
        m_currentSaves.Sort(new Comparison<SaveGame>(CompareSaveDates));
        return m_currentSaves;
    }

    private List<SaveGame> ReadSaveGameFiles()
    {
        return [.. Directory.GetFiles(GetSaveDir(), "*.hsg")
            .Select(f => new SaveGame(GetSaveDir(), Path.GetFileName(f)))
            .OrderByDescending(f => f.Model?.Date)];
    }

    public bool DeleteSaveGame(SaveGame saveGame)
    {
        try
        {
            if (File.Exists(saveGame.FilePath))
                File.Delete(saveGame.FilePath);

            m_currentSaves.Remove(saveGame);
        }
        catch
        {
            return false;
        }

        return true;
    }

    private string GetNewSaveName(bool autoSave, bool quickSave)
    {
        var files = Directory.GetFiles(GetSaveDir(), "*.hsg")
            .Select(Path.GetFileName)
            .WhereNotNull()
            .ToList();

        int number = 0;
        while (true)
        {
            string name = GetSaveName(number, autoSave, quickSave);
            if (files.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                number++;
            else
                return name;
        }
    }

    private static string GetSaveName(int number, bool autoSave, bool quickSave)
    {
        if (autoSave)
            return $"autosave{number}.hsg";
        else if (quickSave)
            return $"quicksave{number}.hsg";
        return $"savegame{number}.hsg";
    }

    private static int CompareSaveDates(SaveGame x, SaveGame y)
    {
        if (x.Model == null)
            return 1;

        if (y.Model == null)
            return -1;

        return y.Model.Date.CompareTo(x.Model.Date);
    }
}
