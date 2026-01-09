// See https://aka.ms/new-console-template for more information

using System.Text.Json;

using var client = new HttpClient();

HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5070/items");
{
	using var response = await client.SendAsync(request);
	string content = await response.Content.ReadAsStringAsync();
	// Deserialize and pretty print
	Console.WriteLine(content);
	var items = JsonSerializer.Deserialize<List<Item>>(content, options: new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	}) ?? new List<Item>();

	if(items.Find(i => i.Id == 10) is Item item1)
	{
		Console.WriteLine($"Item with ID 10 already exists, deleting it first.");
		request = new HttpRequestMessage(HttpMethod.Delete, $"http://localhost:5070/items/{item1.Id}");
		using var deleteResponse = await client.SendAsync(request);
		deleteResponse.EnsureSuccessStatusCode();
		Console.WriteLine("Deleted existing item with ID 10.");
	}

}

request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5070/items");
request.Content = new StringContent(
"""
{
  "id": 10,
  "name": "Test Item",
  "lastProcessed": null
}
""",
System.Text.Encoding.UTF8, "application/json");

{
	using var response = await client.SendAsync(request);
	response.EnsureSuccessStatusCode();
	Console.WriteLine(await response.Content.ReadAsStringAsync());
}

request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5070/items/1");
{
	using var response = await client.SendAsync(request);
	Console.WriteLine(await response.Content.ReadAsStringAsync());
}

{
	request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5070/items/process-transaction/10");
	using var response = await client.SendAsync(request);
	response.EnsureSuccessStatusCode();
	Console.WriteLine("Processed transaction for item with ID 10");
}

request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5070/items/with-logs");
{
	using var response = await client.SendAsync(request);
	response.EnsureSuccessStatusCode();
	Console.WriteLine(await response.Content.ReadAsStringAsync());
}

request = new HttpRequestMessage(HttpMethod.Delete, "http://localhost:5070/items/10");
{
	using var response = await client.SendAsync(request);
	response.EnsureSuccessStatusCode();
	Console.WriteLine("Deleted item with ID 10");
}


class Item
{
	public int Id { get; set; }
	public string? Name { get; set; }
	public DateTime? LastProcessed { get; set; }
}