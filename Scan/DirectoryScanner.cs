using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCoreProject
{
    class DirectoryScanner {

        public string DirectoryToScan {get; private set; }
        private readonly ConcurrentQueue<string> queue  = null;
        private volatile bool scanCompleted = false;

        public DirectoryScanner(string filePath) {

            if(string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException($"Argument {nameof(filePath)} is not valid");
            
            this.DirectoryToScan = filePath;
            queue = new ConcurrentQueue<string>();
        }

        public void Scan(bool useParallel = true) {
            if(useParallel)
                ParallelScan(this.DirectoryToScan);
            else 
                RecursiveScan(this.DirectoryToScan);
        }

        private void ParallelScan(string directory) {

            if(!Directory.Exists(directory)) {
                Console.WriteLine($"Directory not found: {directory}");
                return;
            }

            scanCompleted = false;
            queue.Clear();

            // one item in queue so we have not reached end
            queue.Enqueue(directory);
            var scanCountDownSignal = new CountdownEvent(1);

            var itemAvailableSignal = new ManualResetEventSlim();

            var endOfScanDetector = Task.Run(() => {
                scanCountDownSignal.Wait();
                // no item remaining to put in the queue 
                // so signal end of scan
                scanCompleted = true;
                itemAvailableSignal.Set();
            });

            int totalDirectoryCount = 1;
            // used to make signal only if certain threshold item is reached in queue
            int divisor = Math.Min(4,Environment.ProcessorCount);

            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            var parallelLoopResult = Parallel.For(0,Environment.ProcessorCount, 
                options,
                (index) => {
                    do 
                    {
                        string dir = null;
                        var dequeueSuccess = queue.TryDequeue(out dir);

                        if(dequeueSuccess == false) {
                            // no item in queue so wait for 
                            // item signal
                            itemAvailableSignal.Wait();
                        } 
                        else {

                            try 
                            {
                                var name = new DirectoryInfo(dir).Name;
                                Console.WriteLine(Thread.CurrentThread.ManagedThreadId 
                                    + " -> " + name);

                                var subDirectories = Directory.GetDirectories(dir);
                                var count = 0;

                                foreach(var subDirectory in subDirectories) {
                                    count++;
                                    itemAvailableSignal.Reset();
                                    queue.Enqueue(subDirectory);
                                    scanCountDownSignal.AddCount(1);

                                    if (count%divisor == 0) {
                                        itemAvailableSignal.Set();
                                    }
                                }
                                // count total directories
                                Interlocked.Add(ref totalDirectoryCount,count);
                            }
                            finally {
                                itemAvailableSignal.Reset();
                                scanCountDownSignal.Signal();
                            }
                        } 
                    } while(!scanCompleted);
                }
            );

            endOfScanDetector.Wait();
            Console.WriteLine("End of scan -> Total Dir Count: " + totalDirectoryCount);
        }

        private void RecursiveScan(string directory) {

            if(!Directory.Exists(directory)) {
                Console.WriteLine($"Directory not found: {directory}");
                return;
            }

            scanCompleted = false;
            queue.Clear();
            queue.Enqueue(directory);

            int totalDirectoriesCount = 1;

            string dir;
            while(queue.TryDequeue(out dir)) {

                var name = new DirectoryInfo(dir).Name;
                
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId 
                    + "->" + name);

                var subDirectories = System.IO.Directory.GetDirectories(dir);

                foreach(var subDirectory in subDirectories) {
                    totalDirectoriesCount++;
                    queue.Enqueue(subDirectory);
                }
            }

            scanCompleted = true;
            Console.WriteLine("End of scan -> Total Dir Count: " + totalDirectoriesCount);
        }
    }
}
