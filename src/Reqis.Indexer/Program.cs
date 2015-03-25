using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Reqis.Indexer
{
    public sealed class Program
    {
        static void Main(string[] args)
        {
            var disposable = new CompositeDisposable();
            var rnd = new Random();
            // create a pretend source
            var src = Observable.Interval(TimeSpan.FromSeconds(1)).Select(_ => new {Id = "i" + rnd.Next(9), Name = "n" + rnd.Next(9), Percent = rnd.Next(99)});

            // start indexing on odd/even, decile bucket and specific name!
            // parity-even, parity-odd
            // name-n1, name-n2, name-n3, ... (open ended but max "n9" here)
            // decile-0, decile-1, decile-2, ... decile-9
            var redis = ConnectionMultiplexer.Connect("localhost");
            var db = redis.GetDatabase();
            disposable.Add(src.Subscribe(i =>
            {
                Console.WriteLine("Determining Redis sets for: id:{0}, name:{1}, percent:{2}", i.Id, i.Name, i.Percent);
                var isEven = i.Percent % 2 == 0;
                var paritySet = isEven ? "parity-even" : "parity-odd";
                var decile = Math.Floor(0.1 * i.Percent);
                var decileSet = string.Format("decile-{0}", decile);
                var nameSet = string.Format("name-{0}", i.Name);
                Console.WriteLine("Matching Redis sets: {0}, {1}, {2}", paritySet, decileSet, nameSet);
                
                // now loop through and clear if in wrong set and add to right set in a Redis transaction
                db.SetAddAsync(paritySet, i.Id);
                db.SetAddAsync(decileSet, i.Id);
                db.SetAddAsync(nameSet, i.Id);

            }));
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            disposable.Dispose();
        }
    }
}
