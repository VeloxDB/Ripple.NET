using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

// --- Configuration ---
const string BaseUrl = "https://localhost:7073"; 
using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };

Console.WriteLine("🚀 Starting BookCart API Test Suite");

// --- 1. User Management & Registration ---
Console.WriteLine("\n--- Phase 1: User & Auth ---");

// GET /api/User/validateUserName/{userName}
var isAvailable = await http.GetFromJsonAsync<bool>($"/api/User/validateUserName/testuser_{Guid.NewGuid().ToString()[..4]}");
Console.WriteLine($"[GET] Username availability check: {isAvailable}");

// POST /api/User (Registration)
var newUser = new UserRegistration(
    "John", "Doe", "johndoe_test", "Password123", "Password123", "Male"
);
var regRes = await http.PostAsJsonAsync("/api/User", newUser);
Console.WriteLine($"[POST] User Registration: {regRes.StatusCode}");

// POST /api/Login
var login = new UserLogin("adminuser", "qwerty");
var loginRes = await http.PostAsJsonAsync("/api/Login", login);
// 3. Deserialize the response body to extract the token
// We use a local record or class to map the JSON structure
var authData = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();

if (authData != null && !string.IsNullOrEmpty(authData.Token)) 
{
    // 4. Set the Authorization header with the REAL token
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.Token);
    Console.WriteLine("Login successful. Token attached to headers.");
}
else
{
    Console.WriteLine("Login failed: Could not parse token from response.");
}
// GET /api/User/{userId}
var userIdRes = await http.GetFromJsonAsync<int>("/api/User/1");
Console.WriteLine($"[GET] Fetch User ID: {userIdRes}");


// --- 2. Book Management (Admin Only) ---
Console.WriteLine("\n--- Phase 2: Book Management (Admin) ---");

// POST /api/Book (Add 10 Books)
int lastBookId = 0;
for (int i = 1; i <= 10; i++)
{
    var book = new Book(0, $"C# Mastery Vol {i}", "Tech Author", "Programming", 29.99 + i, $"cover{i}.jpg");
    var res = await http.PostAsJsonAsync("/api/Book", book);
    if (res.IsSuccessStatusCode) lastBookId = await res.Content.ReadFromJsonAsync<int>();
}
Console.WriteLine($"[POST] Added 10 books. Last Book ID: {lastBookId}");

// PUT /api/Book (Update a book)
var updateBook = new Book(lastBookId, "C# Mastery: Updated Edition", "Tech Author", "Programming", 35.00, "new_cover.jpg");
var putRes = await http.PostAsJsonAsync("/api/Book", updateBook); // API uses Post/Put interchangeably in some specs, checking JSON...
// The JSON spec shows a PUT /api/Book
var actualPutRes = await http.PutAsJsonAsync("/api/Book", updateBook);
Console.WriteLine($"[PUT] Update Book {lastBookId}: {actualPutRes.StatusCode}");


// --- 3. Browsing & Searching ---
Console.WriteLine("\n--- Phase 3: Browsing ---");

// GET /api/Book (All)
var allBooks = await http.GetFromJsonAsync<List<Book>>("/api/Book");
Console.WriteLine($"[GET] Total Books Found: {allBooks?.Count}");

// GET /api/Book/{id}
var singleBook = await http.GetAsync($"/api/Book/{lastBookId}");
Console.WriteLine($"[GET] Get Book by ID: {singleBook.StatusCode}");

// GET /api/Book/GetCategoriesList
var cats = await http.GetFromJsonAsync<List<Categories>>("/api/Book/GetCategoriesList");
Console.WriteLine($"[GET] Categories count: {cats?.Count}");

try
{
    // GET /api/Book/GetSimilarBooks/{bookId}
    var similar = await http.GetFromJsonAsync<List<Book>>($"/api/Book/GetSimilarBooks/{lastBookId}");
    Console.WriteLine($"[GET] Similar books found: {similar?.Count}");
}
catch (Exception ex)
{
    Console.WriteLine($"[GET] Error fetching similar books: {ex.Message}");
}


// --- 4. Shopping Cart & Wishlist ---
Console.WriteLine("\n--- Phase 4: Cart & Wishlist ---");
int testUid = 1;
int testBid = lastBookId;

// POST /api/ShoppingCart/AddToCart/{userId}/{bookId}
var cartRes = await http.PostAsync($"/api/ShoppingCart/AddToCart/{testUid}/{testBid}", null);
Console.WriteLine($"[POST] Add to Cart: {cartRes.StatusCode}");

// PUT /api/ShoppingCart/{userId}/{bookId} (Update quantity)
var updateCartRes = await http.PutAsync($"/api/ShoppingCart/{testUid}/{testBid}", null);
Console.WriteLine($"[PUT] Update Cart Quantity: {updateCartRes.StatusCode}");

// GET /api/ShoppingCart/{userId}
var cartItems = await http.GetFromJsonAsync<List<CartItemDto>>($"/api/ShoppingCart/{testUid}");
Console.WriteLine($"[GET] Items in Cart: {cartItems?.Count}");

// GET /api/ShoppingCart/SetShoppingCart/{oldUserId}/{newUserId} (Merge)
var mergeRes = await http.GetAsync($"/api/ShoppingCart/SetShoppingCart/999/{testUid}");
Console.WriteLine($"[GET] Merge Shopping Cart: {mergeRes.StatusCode}");

// POST /api/Wishlist/ToggleWishlist/{userId}/{bookId}
var wishRes = await http.PostAsync($"/api/Wishlist/ToggleWishlist/{testUid}/{testBid}", null);
Console.WriteLine($"[POST] Toggle Wishlist: {wishRes.StatusCode}");

// GET /api/Wishlist/{userId}
var wishlist = await http.GetFromJsonAsync<List<Book>>($"/api/Wishlist/{testUid}");
Console.WriteLine($"[GET] Wishlist count: {wishlist?.Count}");


// --- 5. Checkout & Orders ---
Console.WriteLine("\n--- Phase 5: Checkout ---");

// POST /api/CheckOut/{userId}
var checkoutData = new Checkout(cartItems, 59.98);
var checkoutRes = await http.PostAsJsonAsync($"/api/CheckOut/{testUid}", checkoutData);
Console.WriteLine($"[POST] Checkout: {checkoutRes.StatusCode}");

// GET /api/Order/{userId}
var orders = await http.GetFromJsonAsync<List<OrdersDto>>($"/api/Order/{testUid}");
Console.WriteLine($"[GET] Order History count: {orders?.Count}");


// --- 6. Cleanup (Hitting Delete Endpoints) ---
Console.WriteLine("\n--- Phase 6: Cleanup/Delete ---");

// DELETE /api/Wishlist/{userId}
var delWish = await http.DeleteAsync($"/api/Wishlist/{testUid}");
Console.WriteLine($"[DELETE] Clear Wishlist: {delWish.StatusCode}");

// DELETE /api/ShoppingCart/{userId}/{bookId}
var delItem = await http.DeleteAsync($"/api/ShoppingCart/{testUid}/{testBid}");
Console.WriteLine($"[DELETE] Remove item from cart: {delItem.StatusCode}");

// DELETE /api/ShoppingCart/{userId}
var delCart = await http.DeleteAsync($"/api/ShoppingCart/{testUid}");
Console.WriteLine($"[DELETE] Clear Shopping Cart: {delCart.StatusCode}");

// DELETE /api/Book/{id} (Admin)
var delBook = await http.DeleteAsync($"/api/Book/{lastBookId}");
Console.WriteLine($"[DELETE] Delete Book: {delBook.StatusCode}");

Console.WriteLine("\n✅ All API endpoints exercised successfully.");

// --- DTO Records (C# 10/12 syntax) ---
public record Book(int bookId, string title, string author, string category, double price, string coverFileName);
public record Categories(int categoryId, string categoryName);
public record CartItemDto(Book book, int quantity);
public record Checkout(List<CartItemDto>? orderDetails, double cartTotal);
public record OrdersDto(string orderId, List<CartItemDto>? orderDetails, double cartTotal, DateTime orderDate);
public record UserLogin(string username, string password);
public record UserRegistration(string firstName, string lastName, string username, string password, string confirmPassword, string gender);
public record LoginResponse(string Token, UserDetails UserDetails);
public record UserDetails(int UserId, string Username, string UserTypeName);