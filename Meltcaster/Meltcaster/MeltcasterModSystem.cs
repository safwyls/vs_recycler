﻿using Meltcaster.BlockEntities;
using Meltcaster.Blocks;
using Meltcaster.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Meltcaster
{
    public class MeltcasterModSystem : ModSystem
    {
        public ICoreAPI Api { get; private set; } = null!;
        public static MeltcasterConfig? Config { get; private set; }
        public string ModId => Mod.Info.ModID;
        private FileWatcher? _fileWatcher;
        private bool assetsFinalized = false;

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            Api = api;
            base.Start(api);

            api.RegisterBlockClass(Mod.Info.ModID + ".blockmeltcaster", typeof(BlockMeltcaster));
            api.RegisterBlockEntityClass(Mod.Info.ModID + ".blockentitymeltcaster", typeof(BlockEntityMeltcaster));

            ReloadConfig(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            assetsFinalized = true;
            
            api.Logger.Debug("[Meltcaster] Assets finalized. Loading config...");
            
            // Wait til all assets are loaded to resolve recipes in config
            Config?.ResolveAll(api);
        }

        public void ReloadConfig(ICoreAPI api)
        {
            (_fileWatcher ??= new FileWatcher(this)).Queued = true;

            var _config = api.LoadModConfig<MeltcasterConfig>("meltcaster.json");
            if (_config == null)
            {
                api.Logger.Warning("[Meltcaster] Missing config! Using default.");
                Config = MeltcasterConfig.GetDefault();
                api.StoreModConfig<MeltcasterConfig>(Config, "meltcaster.json");
            }
            else
            {
                Config = _config;
                api.Logger.Notification($"[Meltcaster] Loaded {Config.MeltcastRecipes?.Count ?? 0} meltcasting recipes.");
            }
            
            if (assetsFinalized) Config?.ResolveAll(api);

            Api.Event.RegisterCallback(_ => _fileWatcher.Queued = false, 100);
        }

        public override void Dispose()
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;
        }
    }
}
