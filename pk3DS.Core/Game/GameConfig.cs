﻿using System.IO;
using System.Linq;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures.PersonalInfo;
using pk3DS.Core.Structures;

namespace pk3DS.Core
{
    public class GameConfig
    {
        private const int FILECOUNT_XY = 271;
        private const int FILECOUNT_ORASDEMO = 301;
        private const int FILECOUNT_ORAS = 299;
        private const int FILECOUNT_SMDEMO = 239;
        private const int FILECOUNT_SM = 311; // only a guess for now
        private const int FILECOUNT_USUM = 333; // only a guess for now
        public readonly GameVersion Version = GameVersion.Invalid;

        public GARCReference[] Files { get; private set; }
        public TextVariableCode[] Variables { get; private set; }
        public TextReference[] GameText { get; private set; }
        public GameInfo Info { get; private set; }

        /// <summary>
        /// Whether or not to remap characters in text files to proper unicode. Defaults to false.
        /// </summary>
        /// <remarks>
        /// This will enable the display of ♂ and ♀, but may interfere with text editing. Set this to true if you're not going to change any text, otherwise set it to false.
        /// </remarks>
        public bool RemapCharacters { get; set; } = false;

        public GameConfig(int fileCount)
        {
            GameVersion game = GameVersion.Invalid;
            switch (fileCount)
            {
                case FILECOUNT_XY:
                    game = GameVersion.XY;
                    break;
                case FILECOUNT_ORASDEMO:
                    game = GameVersion.ORASDEMO;
                    break;
                case FILECOUNT_ORAS:
                    game = GameVersion.ORAS;
                    break;
                case FILECOUNT_SMDEMO:
                    game = GameVersion.SMDEMO;
                    break;
                case FILECOUNT_SM:
                    game = GameVersion.SM;
                    break;
                case FILECOUNT_USUM:
                    game = GameVersion.USUM;
                    break;
            }
            if (game == GameVersion.Invalid)
                return;

            Version = game;
        }
        public GameConfig(GameVersion game)
        {
            Version = game;
        }

        private void getGameData(GameVersion game)
        {
            switch (game)
            {
                case GameVersion.XY:
                    Files = GARCReference.GARCReference_XY;
                    Variables = TextVariableCode.VariableCodes_XY;
                    GameText = TextReference.GameText_XY;
                    break;

                case GameVersion.ORASDEMO:
                case GameVersion.ORAS:
                    Files = GARCReference.GARCReference_AO;
                    Variables = TextVariableCode.VariableCodes_AO;
                    GameText = TextReference.GameText_AO;
                    break;

                case GameVersion.SMDEMO:
                    Files = GARCReference.GARCReference_SMDEMO;
                    Variables = TextVariableCode.VariableCodes_SM;
                    GameText = TextReference.GameText_SMDEMO;
                    break;
                case GameVersion.SN:
                case GameVersion.MN:
                case GameVersion.SM:
                    Files = GARCReference.GARCReference_SN;
                    if (new FileInfo(Path.Combine(RomFS, getGARCFileName("encdata"))).Length == 0)
                        Files = GARCReference.GARCReference_MN;
                    Variables = TextVariableCode.VariableCodes_SM;
                    GameText = TextReference.GameText_SM;
                    break;
                case GameVersion.US:
                case GameVersion.UM:
                case GameVersion.USUM:
                    Files = GARCReference.GARCReference_US;
                    if (new FileInfo(Path.Combine(RomFS, getGARCFileName("encdata"))).Length == 0)
                        Files = GARCReference.GARCReference_UM;
                    Variables = TextVariableCode.VariableCodes_SM;
                    GameText = TextReference.GameText_USUM;
                    break;
            }
        }
        public void Initialize(string romFSpath, string exeFSpath, int lang)
        {
            RomFS = romFSpath;
            ExeFS = exeFSpath;
            Language = lang;
            getGameData(Version);
            InitializeAll();
        }
        public void InitializeAll()
        {
            InitializePersonal();
            InitializeLearnset();
            InitializeGameText();
            InitializeMoves();
            InitializeGameInfo();
        }
        public void InitializePersonal()
        {
            GARCPersonal = getGARCData("personal");
            Personal = new PersonalTable(GARCPersonal.getFile(GARCPersonal.FileCount - 1), Version);
        }
        public void InitializeLearnset()
        {
            GARCLearnsets = getGARCData("levelup");
            switch (Generation)
            {
                case 6:
                    Learnsets = GARCLearnsets.Files.Select(file => new Learnset6(file)).ToArray();
                    break;
                case 7:
                    Learnsets = GARCLearnsets.Files.Select(file => new Learnset7(file)).ToArray();
                    break;
            }
        }
        public void InitializeGameText()
        {
            GARCGameText = getGARCData("gametext");
            GameTextStrings = GARCGameText.Files.Select(file => new TextFile(this, file, RemapCharacters).Lines).ToArray();
        }
        public void InitializeMoves()
        {
            GARCMoves = getGARCData("move");
            switch (Generation)
            {
                case 6:
                    if (XY)
                        Moves = GARCMoves.Files.Select(file => new Move(file)).ToArray();
                    if (ORAS)
                        Moves = mini.unpackMini(GARCMoves.getFile(0), "WD").Select(file => new Move(file)).ToArray();
                    break;
                case 7:
                    Moves = mini.unpackMini(GARCMoves.getFile(0), "WD").Select(file => new Move(file)).ToArray();
                    break;
            }
        }
        private void InitializeGameInfo()
        {
            Info = new GameInfo(this);
        }
        public lzGARCFile getlzGARCData(string file)
        {
            var gr = getGARCReference(file);
            gr = gr.LanguageVariant ? gr.getRelativeGARC(Language, gr.Name) : gr;
            return new lzGARCFile(getlzGARC(file), gr, getGARCPath(file));
        }
        public GARCFile getGARCData(string file, bool skipRelative = false)
        {
            var gr = getGARCReference(file);
            if (gr.LanguageVariant && !skipRelative)
                gr = gr.getRelativeGARC(Language, gr.Name);
            return getGARCByReference(gr);
        }
        public GARCFile getGARCByReference(GARCReference gr)
        {
            return new GARCFile(getMemGARC(gr.Name), gr, getGARCPath(gr.Name));
        }
        private string getGARCPath(string file)
        {
            var gr = getGARCReference(file);
            gr = gr.LanguageVariant ? gr.getRelativeGARC(Language, gr.Name) : gr;
            string subloc = gr.Reference;
            return Path.Combine(RomFS, subloc);
        }

        private GARC.MemGARC getMemGARC(string file)
        {
            return new GARC.MemGARC(File.ReadAllBytes(getGARCPath(file)));
        }
        private GARC.lzGARC getlzGARC(string file)
        {
            return new GARC.lzGARC(File.ReadAllBytes(getGARCPath(file)));
        }

        public string RomFS, ExeFS;

        public GARCReference getGARCReference(string name) { return Files.FirstOrDefault(f => f.Name == name); }
        public TextVariableCode getVariableCode(string name) { return Variables.FirstOrDefault(v => v.Name == name); }
        public TextVariableCode getVariableName(int value) { return Variables.FirstOrDefault(v => v.Code == value); }

        private TextReference getGameText(TextName name) { return GameText.FirstOrDefault(f => f.Name == name); }
        public string[] getText(TextName file)
        {
            return (string[])GameTextStrings[getGameText(file).Index].Clone();
        }
        public bool setText(TextName file, string[] strings)
        {
            GameTextStrings[getGameText(file).Index] = strings;
            return true;
        }

        public string getGARCFileName(string requestedGARC)
        {
            var garc = getGARCReference(requestedGARC);
            if (garc.LanguageVariant)
                garc = garc.getRelativeGARC(Language);

            return garc.Reference;
        }

        public int Language { get; set; }

        public GARCFile GARCPersonal, GARCLearnsets, GARCMoves, GARCGameText;
        public PersonalTable Personal { get; private set; }
        public Learnset[] Learnsets { get; private set; }
        public string[][] GameTextStrings { get; private set; }
        public Move[] Moves { get; private set; }

        public bool XY => Version == GameVersion.XY;
        public bool ORAS => Version == GameVersion.ORAS || Version == GameVersion.ORASDEMO;
        public bool SM => Version == GameVersion.SM || Version == GameVersion.SMDEMO;
        public bool USUM => Version == GameVersion.USUM;
        public int MaxSpeciesID => XY || ORAS ? 721 : SM ? 802 : 807;
        public int GARCVersion => XY || ORAS ? GARC.VER_4 : GARC.VER_6;
        public int Generation
        {
            get
            {
                if (XY || ORAS)
                    return 6;
                if (SM || USUM)
                    return 7;
                return -1;
            }
        }

        public bool IsRebuildable(int fileCount)
        {
            switch (fileCount)
            {
                case FILECOUNT_XY:
                    return Version == GameVersion.XY;
                case FILECOUNT_ORAS:
                    return Version == GameVersion.ORAS;
                case FILECOUNT_ORASDEMO:
                    return Version == GameVersion.ORASDEMO;
                case FILECOUNT_SMDEMO:
                    return Version == GameVersion.SMDEMO;
                case FILECOUNT_SM:
                    return Version == GameVersion.SM;
                case FILECOUNT_USUM:
                    return Version == GameVersion.USUM;
            }
            return false;
        }
    }
}
