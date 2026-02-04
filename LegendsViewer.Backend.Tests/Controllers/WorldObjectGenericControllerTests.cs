using System.Collections.Generic;
using System.Linq;
using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.DataAccess.Repositories.Interfaces;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendsViewer.Backend.Tests.Controllers;

[TestClass]
public class WorldObjectGenericControllerTests
{
    [TestMethod]
    public void Get_UsesSearchAliasesForFiltering()
    {
        var world = new World();
        var artifact = new Artifact(new List<Property>(), world)
        {
            Id = 1,
            Name = "Artifact"
        };
        artifact.SetSearchAliases(new[] { "ngathsesh" });

        var repository = new InMemoryArtifactRepository([artifact]);
        var controller = new ArtifactController(repository);

        var result = controller.Get(search: "ngathsesh");
        var okResult = result.Result as OkObjectResult;
        Assert.IsNotNull(okResult);
        var payload = okResult.Value as PaginatedResponse<WorldObjectDto>;
        Assert.IsNotNull(payload);
        Assert.AreEqual(1, payload.Items.Count);
        Assert.AreEqual(artifact.Id, payload.Items[0].Id);
    }

    private sealed class InMemoryArtifactRepository : IWorldObjectRepository<Artifact>
    {
        private readonly List<Artifact> _artifacts;

        public InMemoryArtifactRepository(List<Artifact> artifacts)
        {
            _artifacts = artifacts;
        }

        public List<Artifact> GetAllElements() => _artifacts;

        public Artifact? GetById(int id) => _artifacts.FirstOrDefault(a => a.Id == id);

        public int GetCount() => _artifacts.Count;
    }
}
