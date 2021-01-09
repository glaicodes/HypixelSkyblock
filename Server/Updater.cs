using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Hypixel.NET;
using Hypixel.NET.SkyblockApi;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class Updater
    {
        private const int Miniute = 60000;
        private string apiKey;
        private bool abort;
        private static bool minimumOutput;

        public static DateTime LastPull { get; internal set; }
        public static int UpdateSize { get; internal set; }

        private static ConcurrentDictionary<string, BinInfo> LastUpdateBins = new ConcurrentDictionary<string, BinInfo>();

        public Updater(string apiKey)
        {
            this.apiKey = apiKey;
        }

        /// <summary>
        /// Downloads all auctions and save the ones that changed since the last update
        /// </summary>
        public async Task Update()
        {
            if (!minimumOutput)
                Console.WriteLine($"Usage bevore update {System.GC.GetTotalMemory(false)}");
            var updateStartTime = DateTime.UtcNow.ToLocalTime();

            try
            {
                lastUpdateDone = await RunUpdate(lastUpdateDone);
                FileController.SaveAs("lastUpdate", lastUpdateDone);
                FileController.Delete("lastUpdateStart");
            }
            catch (Exception e)
            {
                Logger.Instance.Error($"Updating stopped because of {e.Message} {e.StackTrace}  {e.InnerException?.Message} {e.InnerException?.StackTrace}");
                FileController.Delete("lastUpdateStart");
                throw e;
            }

            ItemDetails.Instance.Save();

            await StorageManager.Save();
            Console.WriteLine($"Done in {DateTime.Now.ToLocalTime()}");
        }

        DateTime lastUpdateDone = new DateTime(1970, 1, 1);

        async Task<DateTime> RunUpdate(DateTime updateStartTime)
        {
            var hypixel = new HypixelApi(apiKey, 50);
            /* Task.Run(()
                  => BinUpdater.GrabAuctions(hypixel)
             );*/
            BinUpdater.GrabAuctions(hypixel);
            long max = 1;
            var lastUpdate = lastUpdateDone; // new DateTime (1970, 1, 1);
            //if (FileController.Exists ("lastUpdate"))
            //    lastUpdate = FileController.LoadAs<DateTime> ("lastUpdate").ToLocalTime ();

            var lastUpdateStart = new DateTime(0);
            if (FileController.Exists("lastUpdateStart"))
                lastUpdateStart = FileController.LoadAs<DateTime>("lastUpdateStart").ToLocalTime();

            if (!minimumOutput)
                Console.WriteLine($"{lastUpdateStart > lastUpdate} {DateTime.Now - lastUpdateStart}");
            FileController.SaveAs("lastUpdateStart", DateTime.Now);

            Console.WriteLine(updateStartTime);

            TimeSpan timeEst = new TimeSpan(0, 1, 1);
            Console.WriteLine($"Updating Data {DateTime.Now}");

            // add extra miniute to start to catch lost auctions
            lastUpdate = lastUpdate - new TimeSpan(0, 1, 0);
            DateTime timestamp = lastUpdate;

            var tasks = new List<Task>();
            int sum = 0;
            int doneCont = 0;
            object sumloc = new object();
            var firstPage = await hypixel?.GetAuctionPageAsync(0);
            max = firstPage.TotalPages;

            ConcurrentDictionary<string, BinInfo> currentUpdateBins = new ConcurrentDictionary<string, BinInfo>();

            for (int i = 0; i < max; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {

                    try
                    {
                        var res = index != 0 ? await hypixel?.GetAuctionPageAsync(index) : firstPage;
                        if (res == null)
                            return;

                        max = res.TotalPages;

                        if (index == 0)
                        {
                            timestamp = res.LastUpdated;
                            // correct update time
                            Console.WriteLine($"Updating difference {lastUpdate} {res.LastUpdated}\n");
                        }

                        // spread out the saving load burst
                        //Thread.Sleep(index * 300);


                        var val = Save(res, lastUpdate, currentUpdateBins);
                        lock (sumloc)
                        {
                            sum += val;
                            // process done
                            doneCont++;
                        }
                        PrintUpdateEstimate(index, doneCont, sum, updateStartTime, max);
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error($"Single page ({index}) could not be loaded because of {e.Message} {e.StackTrace}");
                    }

                }));
                PrintUpdateEstimate(i, doneCont, sum, updateStartTime, max);

                // try to stay under 150MB
                if (System.GC.GetTotalMemory(false) > 150000000)
                {
                    Console.Write("\t mem: " + System.GC.GetTotalMemory(false));
                    System.GC.Collect();
                }
            }

            foreach (var item in tasks)
            {
                //Console.Write($"\r {index++}/{updateEstimation} \t({index}) {timeEst:mm\\:ss}");
                if (item != null)
                    await item;
                PrintUpdateEstimate(max, doneCont, sum, updateStartTime, max);
            }

            //BinUpdateSold(currentUpdateBins);

            if (sum > 10)
                LastPull = DateTime.Now;

            Console.WriteLine($"Updated {sum} auctions {doneCont} pages");
            UpdateSize = sum;

            return timestamp;
        }

        private void BinUpdateSold(ConcurrentDictionary<string, BinInfo> currentUpdateBins)
        {
            foreach (var item in currentUpdateBins)
            {
                LastUpdateBins.TryRemove(item.Key, out BinInfo time);
            }

            var bought = LastUpdateBins.Where(item => item.Value.End > lastUpdateDone).ToList();

            Console.WriteLine($"Bought {bought.Count()}, expired {LastUpdateBins.Count() - bought.Count()}, TotalBinCount {currentUpdateBins.Count()} - ");

            var updater = new BinUpdater(SimplerConfig.Config.Instance["apiKeys"].Split(','));

            Task.Run(()
                 => updater.GrabAuctionsWithIds(bought.Select(a => a.Value))
            );

            LastUpdateBins = currentUpdateBins;
        }

        internal void UpdateForEver()
        {
            Task.Run(async () =>
            {
                minimumOutput = true;
                while (true)
                {
                    try
                    {
                        var start = DateTime.Now;
                        Task.Run(() => Update());
                        if (abort)
                        {
                            Console.WriteLine("Stopped updater");
                            break;
                        }
                        await WaitForServerCacheRefresh(start);
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error("Updater encountered an outside error " + e.Message);
                        Thread.Sleep(10000);
                    }

                }
            });
        }

        private static async Task WaitForServerCacheRefresh(DateTime start)
        {
            var timeToSleep = start.Add(TimeSpan.FromSeconds(59.5)) - DateTime.Now;
            if (timeToSleep.Seconds > 0)
                await Task.Delay(timeToSleep);
        }

        static void PrintUpdateEstimate(long i, long doneCont, long sum, DateTime updateStartTime, long max)
        {
            var index = sum;
            // max is doubled since it is counted twice (download and done)
            var updateEstimation = index * max * 2 / (i + 1 + doneCont) + 1;
            var ticksPassed = (DateTime.Now.ToLocalTime().Ticks - updateStartTime.Ticks);
            var timeEst = new TimeSpan(ticksPassed / (index + 1) * updateEstimation - ticksPassed);
            if (!minimumOutput)
                Console.Write($"\r Loading: ({i}/{max}) Done With: {doneCont} Total:{sum} {timeEst:mm\\:ss}");
        }

        // builds the index for all auctions in the last hour

        static int Save(GetAuctionPage res, DateTime lastUpdate, ConcurrentDictionary<string, BinInfo> currentUpdateBins)
        {
            int count = 0;

            var processed = res.Auctions.Where(item =>
                {
                    ItemDetails.Instance.AddOrIgnoreDetails(item);
                    if (item.BuyItNow)
                        currentUpdateBins.AddOrUpdate(item.Uuid, new BinInfo() { End = item.End, Auctioneer = item.Auctioneer }, (UuId, end) => end);


                    // nothing changed if the last bid is older than the last update
                    return !(item.Bids.Count > 0 && item.Bids[item.Bids.Count - 1].Timestamp < lastUpdate ||
                        item.Bids.Count == 0 && item.Start < lastUpdate);
                })
                .Select(a =>
                {
                    count++;
                    return new SaveAuction(a);
                });

            var ended = res.Auctions.Where(a => a.End < DateTime.Now).Select(a => new SaveAuction(a));
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(20));
                foreach (var item in ended)
                {
                    ItemPrices.Instance.AddNewAuction(item);
                }
            });


            if (Program.FullServerMode)
                Indexer.AddToQueue(processed);
            else
                FileController.SaveAs($"apull/{DateTime.Now.Ticks}", processed);

            var started = processed.Where(a => a.Start > DateTime.Now - TimeSpan.FromMinutes(2));
            foreach (var item in started)
            {
                SubscribeEngine.Instance.NewAuction(item);
            }

            return count;
        }

        internal void Stop()
        {
            abort = true;
        }
    }
}