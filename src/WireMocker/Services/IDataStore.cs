using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RZ.Foundation.Extensions;
using RZ.Foundation.Json;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Services;

public interface IDataStore
{
    OutcomeT<Synchronous, IAsyncEnumerable<Service>> GetServices();

    OutcomeT<Asynchronous, Service>      Save(Service service);
    OutcomeT<Asynchronous, Seq<Service>> Search(string name);

    Stream        SnapshotData(Unit _);
    OutcomeT<Asynchronous, Unit> LoadFromSnapshot(Stream snapshot);
}

public sealed class InMemoryDataStore : IDataStore
{
    ConcurrentDictionary<Guid, Service> services = new();
    ConcurrentDictionary<Guid, ServiceSetting> serviceSettings = new();

    public OutcomeT<Synchronous, IAsyncEnumerable<Service>> GetServices() =>
        Success(services.Values.ToArray().AsAsyncEnumerable());

    public OutcomeT<Asynchronous, Service> Save(Service service) {
        var invalidReason = Service.Validator.Validate(service).Errors.Map(e => e.ToString()).HeadOrNone();
        return invalidReason.IfSome(out var error)
                   ? FailureAsync<Service>(StandardErrors.UnexpectedError($"Invalid service data: {error}"))
                   : SuccessAsync(services[service.Id] = service);
    }

    public OutcomeT<Asynchronous, Seq<Service>> Search(string name) {
        var result = services.Values.Where(s => s.Name.Contains(name)).ToSeq();
        return SuccessAsync(result);
    }

    public Stream SnapshotData(Unit _) {
        var snapshot = new Snapshot(services.Values.ToArray(), serviceSettings.Values.ToArray());
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
            serviceSettings = new(snapshot.ServiceSettings.Map(s => KeyValuePair.Create(s.Id, s)));
        }
    }

    readonly record struct Snapshot(Service[] Services, ServiceSetting[] ServiceSettings);

    static readonly JsonSerializerOptions JsonOptions = new() {
        Converters = { MapJsonConverter.Default, SeqJsonConverter.Default, OptionJsonConverter.Default }
    };
}