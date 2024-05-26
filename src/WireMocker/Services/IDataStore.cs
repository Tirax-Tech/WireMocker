using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RZ.Foundation;
using RZ.Foundation.Extensions;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Services;

public interface IDataStore
{
    OutcomeT<Synchronous, IAsyncEnumerable<Service>> GetServices();

    OutcomeT<Asynchronous, Service>      Save(Service service);
    OutcomeT<Asynchronous, Seq<Service>> Search(string name);

    Stream        SnapshotData(Unit _);
    Outcome<Unit> LoadFromSnapshot(Stream snapshot);
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
        var json = JsonSerializer.Serialize(snapshot);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public Outcome<Unit> LoadFromSnapshot(Stream snapshotData) {
        try{
            var json = new StreamReader(snapshotData).ReadToEnd();
            var snapshot = JsonSerializer.Deserialize<Snapshot>(json);
            services = new(snapshot.Services.Map(s => KeyValuePair.Create(s.Id, s)));
            serviceSettings = new(snapshot.ServiceSettings.Map(s => KeyValuePair.Create(s.Id, s)));
            return unitOutcome;
        }
        catch (Exception e){
            return Failure<Unit>(e).RunIO();
        }
    }

    readonly record struct Snapshot(Service[] Services, ServiceSetting[] ServiceSettings);
}