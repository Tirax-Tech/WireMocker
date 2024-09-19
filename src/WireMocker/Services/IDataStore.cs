using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using RZ.Foundation;
using RZ.Foundation.Json;
using RZ.Foundation.Types;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Services;

public interface IDataStore
{
    Service[] GetServices();
    Service?     RemoveService(Guid serviceId);

    IObservable<Service>    Save(Service service);
    ValueTask<Service[]> Search(string name);

    Stream    SnapshotData();
    ValueTask LoadFromSnapshot(Stream snapshot);
}

public sealed class InMemoryDataStore : IDataStore
{
    ConcurrentDictionary<Guid, Service> services = new();

    public Service[] GetServices()
        => services.Values.ToArray();

    public Service? RemoveService(Guid serviceId)
        => services.TryRemove(serviceId, out var service) ? service : null;

    public IObservable<Service> Save(Service service) {
        var invalidReason = Service.Validator.Validate(service).Errors.Map(e => e.ToString()).HeadOrNone();
        return invalidReason.IfSome(out var error)
                   ? Observable.Throw<Service>(new ErrorInfoException(StandardErrorCodes.InvalidResponse, $"Invalid service data: {error}"))
                   : Observable.Return(services[service.Id] = service);
    }

    public ValueTask<Service[]> Search(string name)
        => new(services.Values.Where(s => s.Name.Contains(name)).ToArray());

    public Stream SnapshotData() {
        var snapshot = new Snapshot(services.Values.ToArray());
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public async ValueTask LoadFromSnapshot(Stream snapshotData) {
        var json = await new StreamReader(snapshotData).ReadToEndAsync();
        var snapshot = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions);
        services = new(snapshot.Services.Map(s => KeyValuePair.Create(s.Id, s)));
    }

    readonly record struct Snapshot(Service[] Services);

    static readonly JsonSerializerOptions JsonOptions = new() {
        Converters = { MapJsonConverter.Default, SeqJsonConverter.Default, OptionJsonConverter.Default }
    };
}