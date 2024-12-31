using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Helion.Models;
using Helion.Resources.Archives.Collection;
using Helion.Util;
using Helion.Util.Configs;
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
    private readonly List<SaveGame> m_matchingSaves = [];
    private readonly Comparison<SaveGame> m_saveDateComparison = new(CompareSaveDates);
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
        
        m_currentSaves.AddRange(ReadSaveGameFiles());
        m_matchingSaves.AddRange(GetMatchingSaveGames(m_currentSaves));
        m_currentSavesLoaded = true;
    }

    public bool SaveFileExists(string filename)
    {
        string filePath = Path.Combine(GetSaveDir(), filename);
        return File.Exists(filePath);
    }

    public SaveGame ReadSaveGame(string filename) => new(GetSaveDir(), filename);

    public Task<SaveGameEvent> WriteNewSaveGameAsync(IWorld world, string title, IScreenshotGenerator screenshotGenerator, SaveGameType type = SaveGameType.Default) =>
        WriteSaveGameAsync(world, title, screenshotGenerator, null, type);

    public async Task<SaveGameEvent> WriteSaveGameAsync(IWorld world, string title, IScreenshotGenerator screenshotGenerator, SaveGame? existingSave, SaveGameType type = SaveGameType.Default)
    {
        existingSave = GetExistingSave(existingSave, type);
        var filename = existingSave?.FileName ?? GetNewSaveName(type);
        var worldModel = world.ToWorldModel();
        var image = screenshotGenerator.GetImage();
        var saveEvent = await Task.Run(() => SaveGame.WriteSaveGame(world, worldModel, title, GetSaveDir(), filename, screenshotGenerator, image));

        AddOrUpdateSaveGame(saveEvent.SaveGame);
        GameSaved?.Invoke(this, saveEvent);
        return saveEvent;
    }

    public SaveGameEvent WriteNewSaveGame(IWorld world, string title, IScreenshotGenerator screenshotGenerator, SaveGameType type = SaveGameType.Default) =>
        WriteSaveGame(world, title, screenshotGenerator, null, type);

    public SaveGameEvent WriteSaveGame(IWorld world, string title, IScreenshotGenerator screenshotGenerator, SaveGame? existingSave, SaveGameType type = SaveGameType.Default)
    {
        existingSave = GetExistingSave(existingSave, type);
        var filename = existingSave?.FileName ?? GetNewSaveName(type);
        var worldModel = world.ToWorldModel();
        var image = screenshotGenerator.GetImage();
        var saveEvent = SaveGame.WriteSaveGame(world, worldModel, title, GetSaveDir(), filename, screenshotGenerator, image);

        AddOrUpdateSaveGame(saveEvent.SaveGame);
        GameSaved?.Invoke(this, saveEvent);
        return saveEvent;
    }

    private void AddOrUpdateSaveGame(SaveGame newSaveGame)
    {
        AddOrUpdateSaveGame(newSaveGame, m_currentSaves);
        AddOrUpdateSaveGame(newSaveGame, m_matchingSaves);
    }

    private static void AddOrUpdateSaveGame(SaveGame newSaveGame, List<SaveGame> saveList)
    {
        for (int i = 0; i < saveList.Count; i++)
        {
            var save = saveList[i];
            if (save.Type == newSaveGame.Type && save.FileName == newSaveGame.FileName)
            {
                saveList[i] = newSaveGame;
                return;
            }
        }

        saveList.Add(newSaveGame);
    }

    private SaveGame? GetExistingSave(SaveGame? existingSave, SaveGameType type)
    {
        var saveGames = GetSaveGames(sortByDate: false);

        if (existingSave == null && type == SaveGameType.Auto && m_config.Game.RotatingAutoSaves > 0)
        {
            var autoSaves = saveGames.Where(x => x.Type == SaveGameType.Auto).OrderBy(x => x.Model?.Date);
            if (autoSaves.Any() && autoSaves.Count() >= m_config.Game.RotatingAutoSaves)
                existingSave = autoSaves.First();
        }

        if (existingSave == null && type == SaveGameType.Quick && m_config.Game.RotatingQuickSaves > 0)
        {
            var quickSaves = saveGames.Where(x => x.Type == SaveGameType.Quick).OrderBy(x => x.Model?.Date);
            if (quickSaves.Any() && quickSaves.Count() >= m_config.Game.RotatingQuickSaves)
                existingSave = quickSaves.First();
        }

        return existingSave;
    }

    private IEnumerable<SaveGame> GetMatchingSaveGames(List<SaveGame> saveGames)
    {
        return saveGames.Where(x => x.Model != null &&
            ModelVerification.VerifyModelFiles(x.Model.Files, m_archiveCollection, null));
    }

    public List<SaveGame> GetSaveGames(bool sortByDate = true)
    {
        LoadCurrentSaveFiles();
        if (sortByDate)
            m_matchingSaves.Sort(m_saveDateComparison);
        return m_matchingSaves;
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

    private string GetNewSaveName(SaveGameType type)
    {
        int number = 0;
        var searchSaves = m_currentSaves.Where(x => x.Type == type);
        while (true)
        {
            string name = GetSaveName(number, type);
            if (searchSaves.Any(x => x.FileName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                number++;
            else
                return name;
        }
    }

    private static string GetSaveName(int number, SaveGameType type)
    {
        return type switch
        {
            SaveGameType.Auto => $"{SaveGame.AutoPrefix}{number}.hsg",
            SaveGameType.Quick => $"{SaveGame.QuickPrefix}{number}.hsg",
            _ => $"{SaveGame.DefaultPrefix}{number}.hsg",
        };
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
