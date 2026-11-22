
using System.Net;
using System.Net.Http.Json;
using TodoApi.Models;

namespace TodoApi.Tests;

public class TodosApiTests
{
    // ==================================
    // GET ALL
    // ==================================

    [Test]
    public async Task GetAll_ShouldReturnOkAndEmptyList()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/todos");

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        var todos = await response.Content
            .ReadFromJsonAsync<List<Todo>>();

        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Is.Empty);
    }

    // ==================================
    // GET BY ID
    // ==================================

    [Test]
    public async Task GetById_ShouldReturnTodo()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // Create data first
        var createResponse = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Belajar C#",
                isCompleted = false
            }
        );

        var created = await createResponse.Content
            .ReadFromJsonAsync<Todo>();

        // Get by ID
        var response = await client.GetAsync(
            $"/api/todos/{created!.Id}"
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        var todo = await response.Content
            .ReadFromJsonAsync<Todo>();

        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Title, Is.EqualTo("Belajar C#"));
    }

    [Test]
    public async Task GetById_WhenNotFound_ShouldReturn404()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/todos/999");

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ==================================
    // POST / CREATE
    // ==================================

    [Test]
    public async Task Create_ShouldReturnCreated()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Belajar ASP.NET Core",
                isCompleted = false
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created));

        Assert.That(response.Headers.Location, Is.Not.Null);

        var todo = await response.Content
            .ReadFromJsonAsync<Todo>();

        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Id, Is.GreaterThan(0));
        Assert.That(todo.Title, Is.EqualTo("Belajar ASP.NET Core"));
    }

    // ==================================
    // PUT / UPDATE
    // ==================================

    [Test]
    public async Task Update_ShouldReturnNoContentAndPersistChanges()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Belajar C#",
                isCompleted = false
            }
        );

        var created = await createResponse.Content
            .ReadFromJsonAsync<Todo>();

        // Update
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new
            {
                title = "Belajar ASP.NET Core",
                isCompleted = true
            }
        );

        Assert.That(updateResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        // Verify persistence
        var getResponse = await client.GetAsync(
            $"/api/todos/{created.Id}"
        );

        var updated = await getResponse.Content
            .ReadFromJsonAsync<Todo>();

        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Title,
            Is.EqualTo("Belajar ASP.NET Core"));
        Assert.That(updated.IsCompleted, Is.True);
    }

    [Test]
    public async Task Update_WhenNotFound_ShouldReturn404()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/todos/999",
            new
            {
                title = "Tugas baru",
                isCompleted = true
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ==================================
    // DELETE
    // ==================================

    [Test]
    public async Task Delete_ShouldReturnNoContentAndRemoveTodo()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Tugas yang akan dihapus",
                isCompleted = false
            }
        );

        var created = await createResponse.Content
            .ReadFromJsonAsync<Todo>();

        // Delete
        var deleteResponse = await client.DeleteAsync(
            $"/api/todos/{created!.Id}"
        );

        Assert.That(deleteResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deletion
        var getResponse = await client.GetAsync(
            $"/api/todos/{created.Id}"
        );

        Assert.That(getResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_WhenNotFound_ShouldReturn404()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/todos/999");

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ==================================
    // VALIDATION
    // ==================================

    [Test]
    public async Task Create_WithEmptyTitle_ShouldReturn400()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "",
                isCompleted = false
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Create_WithShortTitle_ShouldReturn400()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "AB",
                isCompleted = false
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Create_InvalidData_ShouldNotPersistTodo()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "",
                isCompleted = false
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));

        var getResponse = await client.GetAsync("/api/todos");

        var todos = await getResponse.Content
            .ReadFromJsonAsync<List<Todo>>();

        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Is.Empty);
    }
}
