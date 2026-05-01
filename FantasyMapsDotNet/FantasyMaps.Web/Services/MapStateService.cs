using FantasyMaps.Core.Mesh;

namespace FantasyMaps.Web.Services;

public class MapStateService
{
    private VoronoiMesh? _sharedMesh;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<VoronoiMesh> EnsureMeshAsync(int npts = 4096)
    {
        if (_sharedMesh != null)
            return _sharedMesh;

        await _lock.WaitAsync();
        try
        {
            _sharedMesh ??= await Task.Run(() => MeshBuilder.GenerateGoodMesh(npts));
            return _sharedMesh;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void ResetMesh() => _sharedMesh = null;
}
