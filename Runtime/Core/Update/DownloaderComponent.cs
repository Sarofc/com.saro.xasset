using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Saro.XAsset.Update
{
    public sealed class DownloaderComponent : FEntity
    {
        internal void Start()
        {
            
        }

        internal void Update()
        {
            Download.UpdateAll();
        }

        internal void Destroy()
        {
            Download.ClearAllDownloads();
        }
    }

    [FObjectSystem]
    internal class DownloaderComponentStartSystem : StartSystem<DownloaderComponent>
    {
        public override void Start(DownloaderComponent self)
        {
            self.Start();
        }
    }

    [FObjectSystem]
    internal class DownloaderComponentUpdateSystem : UpdateSystem<DownloaderComponent>
    {
        public override void Update(DownloaderComponent self)
        {
            self.Update();
        }
    }

    [FObjectSystem]
    internal class DownloaderComponentDestroySystem : DestroySystem<DownloaderComponent>
    {
        public override void Destroy(DownloaderComponent self)
        {
            self.Destroy();
        }
    }


}