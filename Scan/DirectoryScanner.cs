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

        public ConcurrentQueue<string> Queue {get; private set;} 
            = new ConcurrentQueue<string>();

        public DirectoryScanner(string filePath) {
            this.DirectoryToScan = filePath ?? throw new Exception("Empty file path");
        }

        public void Scan(bool useParallel = true) {
            if(useParallel)
                ParallelScan(this.DirectoryToScan);
            else 
                RecursiveScan(this.DirectoryToScan);
        }
        private volatile bool ScanCompleted = false;

        private void ParallelScan(string directory) {


            if(!Directory.Exists(directory)) {
                Console.WriteLine($"Directory not found: {directory}");
                return;
            }

            ScanCompleted = false;
            Queue.Clear();

            // one item in queue so we have not reached end
            Queue.Enqueue(directory);
            var countDownEvent = new CountdownEvent(1);

            var manualResetEventSlim = new ManualResetEventSlim();

            var endOfScanDetector = Task.Run(() => {
                countDownEvent.Wait();
                ScanCompleted = true;
                manualResetEventSlim.Set();
            });

            var tasks = new List<Task>();

            int totalCount = 1;
            int divisor = Math.Min(4,Environment.ProcessorCount);

            var factory = new TaskFactory();

            for(int i =0; i < Environment.ProcessorCount; i++) {

                var task = new Task( () => {
                    do 
                    {
                        string dir = null;
                        var dequeueSuccess = Queue.TryDequeue(out dir);

                        if(dequeueSuccess == false) {
                            manualResetEventSlim.Wait();
                            Console.WriteLine(countDownEvent.CurrentCount + ":" + Thread.CurrentThread.ManagedThreadId + "Found nothing" + DateTime.Now);
                        } else {

                            try 
                            {
                                var name = new DirectoryInfo(dir).Name;
                                Console.WriteLine(Thread.CurrentThread.ManagedThreadId 
                                    + "->" + name);

                                var subDirectories = Directory.GetDirectories(dir);
                                var count = 0;

                                foreach(var subDirectory in subDirectories) {
                                    count++;
                                    manualResetEventSlim.Reset();
                                    Queue.Enqueue(subDirectory);
                                    countDownEvent.AddCount(1);

                                    if (count%divisor == 0) {
                                        manualResetEventSlim.Set();
                                    }
                                }

                                Interlocked.Add(ref totalCount,count);
                            }
                            finally {
                                manualResetEventSlim.Reset();
                                countDownEvent.Signal();
                            }
                        } 
                    } while(!ScanCompleted);
                });
                
                task.Start();
                tasks.Add(task);
            }

            endOfScanDetector.Wait();

            foreach(var task in tasks)
                task.Wait();

            Console.WriteLine("End of scan: " + totalCount);
        }

        private void RecursiveScan(string directory) {

            if(!Directory.Exists(directory)) {
                            Console.WriteLine($"Directory not found: {directory}");
                            return;
            }

            ScanCompleted = false;
            Queue.Clear();
            Queue.Enqueue(directory);

            int totalCount = 1;

            string dir;
            while(Queue.TryDequeue(out dir)) {

                var name = new DirectoryInfo(dir).Name;
                
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId 
                    + "->" + name);

                var subDirectories = System.IO.Directory.GetDirectories(dir);

                foreach(var subDirectory in subDirectories) {
                    totalCount++;
                    Queue.Enqueue(subDirectory);
                }
            }

            ScanCompleted = true;

            Console.WriteLine("End of scan: " + totalCount);
        }
    }
}
