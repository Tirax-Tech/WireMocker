using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using RZ.Foundation.Json;
using RZ.Foundation.Observable;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Services;

public interface IDataStore
{
    IObservable<Service>           GetServices();
    OutcomeT<Synchronous, Service> RemoveService(Guid serviceId);

    IObservable<Service>                 Save(Service service);
    OutcomeT<Asynchronous, Seq<Service>> Search(string name);

    Stream                       SnapshotData(Unit _);
    OutcomeT<Asynchronous, Unit> LoadFromSnapshot(Stream snapshot);
}

public sealed class InMemoryDataStore : IDataStore
{
    ConcurrentDictionary<Guid, Service> services = new();

    public IObservable<Service> GetServices() =>
        services.Values.ToObservable();

    public OutcomeT<Synchronous, Service> RemoveService(Guid serviceId) {
        if (services.TryRemove(serviceId, out var service)){
            return Success(service);
        }
        else
            return Failure<Service>(StandardErrors.NotFound);
    }

    public IObservable<Service> Save(Service service) {
                Console.WriteLine($"Saving {service}");
        var invalidReason = Service.Validator.Validate(service).Errors.Map(e => e.ToString()).HeadOrNone();
        return invalidReason.IfSome(out var error)
                   ? Observable.Throw<Service>(new ErrorInfoException(StandardErrors.UnexpectedCode, $"Invalid service data: {error}"))
                   : Observable.Return(services[service.Id] = service);
    }

    public OutcomeT<Asynchronous, Seq<Service>> Search(string name) {
        var result = services.Values.Where(s => s.Name.Contains(name)).ToSeq();
        return SuccessAsync(result);
    }

    public Stream SnapshotData(Unit _) {
        var snapshot = new Snapshot(services.Values.ToArray());
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public OutcomeT<Asynchronous, Unit> LoadFromSnapshot(Stream snapshotData) {
        return from json in TryCatch(() => new StreamReader(snapshotData).ReadToEndAsync())
               from ____ in TryCatch(() => JsonSerializer.Deserialize<Snapshot>(json, JsonOptions))
                          | @do<Snapshot>(Load)
               select unit;

        void Load(Snapshot snapshot) {
            services = new(snapshot.Services.Map(s => KeyValuePair.Create(s.Id, s)));
        }
    }

    readonly record struct Snapshot(Service[] Services);

    static readonly JsonSerializerOptions JsonOptions = new() {
        Converters = { MapJsonConverter.Default, SeqJsonConverter.Default, OptionJsonConverter.Default }
    };
}