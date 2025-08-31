using System.Net;
using System.Text;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace MapFavoritesAutoTests;

public class FavoritesUnitTester : IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly HttpClient _client;
    private readonly HttpClientHandler _clientHandler;
    private readonly CookieContainer _cookieContainer;

    public FavoritesUnitTester(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _cookieContainer = new CookieContainer();
        _clientHandler = new HttpClientHandler { CookieContainer = _cookieContainer, UseCookies = true };
        _client = new HttpClient(_clientHandler) { BaseAddress = new Uri("https://regions-test.2gis.com/") };
    }

    public void Dispose()
    {
        _client.Dispose();
        _clientHandler.Dispose();
    }

    private async Task<Cookie> GetSessionCookie()
    {
        /*
         * Метод для получения куки от сервера.
         */
        var response = await _client.PostAsync("/v1/auth/tokens", null);
        response.EnsureSuccessStatusCode();
        
        var cookieCollection = _cookieContainer.GetCookies(new Uri("https://regions-test.2gis.com/v1/auth/tokens"));
        return cookieCollection.First();
    }
    
    [Fact]
    public async Task API_Should_Add_Favorite_Spot_After_Getting_A_Token()
    {
        /*
         * Проверка работоспособности API при наличии токена.
         */
        var sessionCookie = await GetSessionCookie();
        const string requestData = "title=Favorite Spot&lat=24.24&lon=90.0";
        
        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");
        
        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var responseContent = JsonConvert.DeserializeObject<FavoriteResponse>(responseString);
        
        Assert.Equal("Favorite Spot", responseContent?.Title);
        Assert.Equal(24.24, responseContent?.Lat);
        Assert.Equal(90.0, responseContent?.Lon);
        Assert.Null(responseContent?.Color);
        Assert.NotNull(responseContent?.Id);
        Assert.NotNull(responseContent?.Created_At);
        
        _testOutputHelper.WriteLine($"Response: {responseString}");
    }
    
    [Fact]
    public async Task API_Should_Not_Be_Able_To_Add_Favorite_Spot_Without_A_Token()
    {
        /*
         * Проверка выбрасывания HTTP-статуса Unauthorized при отсутствии токена аутентификации.
         */
        const string requestData = "title=Favorite Spot&lat=27.27&lon=27.27";
        
        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");
        
        request.Content = content;

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task API_Should_Not_Be_Able_To_Add_A_Spot_After_Token_Expires()
    {
        /*
         * Проверка уничтожения токена доступа при истечении 2-х секунд с момента выдачи.
         */
        var sessionCookie = await GetSessionCookie();
        const string requestData = "title=Favorite Spot&lat=27.2638&lon=34.2765";
        
        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        
        await Task.Delay(TimeSpan.FromMilliseconds(3000));
        
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");
        
        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task API_Should_Return_Bad_Request_If_Title_Is_Not_Present()
    {
        /*
         * Проверка получения BadRequest при отсутствии названия отмечаемого места.
         */
        var sessionCookie = await GetSessionCookie();
        const string requestData = "title=&lat=50.0&lon=50.0";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task API_Should_Return_Bad_Request_If_Latitude_Is_Not_Present()
    {
        /*
         * Проверка получения BadRequest при отсутствии широты отмечаемого места.
         */
        var sessionCookie = await GetSessionCookie();
        const string requestData = "title=Favorite Spot&lat=&lon=50.0";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task API_Should_Return_Bad_Request_If_Longitude_Is_Not_Present()
    {
        /*
         * Проверка получения BadRequest при отсутствии долготы отмечаемого места.
         */
        var sessionCookie = await GetSessionCookie();
        const string requestData = "title=Favorite Spot&lat=50.0&lon=";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public async Task API_Should_Return_Bad_Request_If_Title_Is_Empty(string title)
    {
        /*
         * Проверка имени отмечаемого места на пустоту.
         * Тест не пройден при длине названия меньше 1 символа, а также в том случае, если название - пустое логически
         * в случае применения одних только пробелов.
         */
        var sessionCookie = await GetSessionCookie();
        var requestData = $"title={title}&lat=20.0&lon=20.0";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Theory]
    [InlineData("Favorite Spot")]
    [InlineData("Избра++//^^%{}нное Место")]
    [InlineData("ул. Пушкина, д. 17, к. 2")]
    [InlineData(".")]
    [InlineData("583948920138")]
    [InlineData("Plushies & More! 100% (Best) @place#")]
    public async Task API_Should_Support_Specified_Title_Formats(string title)
    {
        /*
         * Проверка допустимых форматов для описания названия отмечаемого места.
         * Тест не проходит проверку при использовании символов & и +, т.к. из-за формата передачи данных между
         * клиентом и сервером (form data) эти символы меняют исходную строку с названием:
         *  1) после & все удаляется;
         *  2) + заменяется на пустую строку / пробел.
         */
        var sessionCookie = await GetSessionCookie();
        var requestData = $"title={title}&lat=1.0&lon=1.0";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var responseContent = JsonConvert.DeserializeObject<FavoriteResponse>(responseString);
        
        Assert.Equal(title, responseContent?.Title);
        Assert.Equal(1.0, responseContent?.Lat);
        Assert.Equal(1.0, responseContent?.Lon);
        Assert.Null(responseContent?.Color);
        
        _testOutputHelper.WriteLine($"Response: {responseString}");
    }
    
    [Fact]
    public async Task API_Should_Create_Favorite_With_Max_Length_Title()
    {
        /*
         * Проверка возможности задать название максимально возможной длины по спецификации.
         */
        var sessionCookie = await GetSessionCookie();
        var maxLengthTitle = new string('a', 999);
        var requestData = $"title={maxLengthTitle}&lat=50.0&lon=50.0";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    
        var responseString = await response.Content.ReadAsStringAsync();
        var responseContent = JsonConvert.DeserializeObject<FavoriteResponse>(responseString);
    
        Assert.NotNull(responseContent);
        Assert.Equal(maxLengthTitle, responseContent.Title);
        Assert.Equal(999, responseContent.Title.Length);
        
        _testOutputHelper.WriteLine($"Response: {responseString}");
    }
    
    [Fact]
    public async Task API_Should_Not_Create_Favorite_With_Unspecified_Length_Title()
    {
        /*
         * Проверка отсутствия возможности обойти спецификацию при выборе длины названия отмечаемого места.
         * Тест не пройден: можно создать название длиной 1000 символов, хотя 999 - максимум.
         */
        var sessionCookie = await GetSessionCookie();
        var maxLengthTitle = new string('a', 1000);
        var requestData = $"title={maxLengthTitle}&lat=50.0&lon=50.0";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task API_Should_Not_Allow_To_Create_Spots_With_Wrong_Alphabet()
    {
        /*
         * Проверка отсутствия возможности использовать в названии мест сторонний алфавит.
         * Например, китайский, а также эмодзи.
         * Тест не проходит проверку.
         */
        var sessionCookie = await GetSessionCookie();
        const string requestData = "title=测试 \ud83d\ude0a&lat=25.25&lon=25.25";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(90.0001, 125.65)]
    [InlineData(-90.0001, 125.65)]
    [InlineData(-1000.0001, 125.65)]
    [InlineData(-2000.0001, 125.65)]
    [InlineData(double.PositiveInfinity, 0)] 
    public async Task API_Should_Return_Bad_Request_If_Latitude_Is_Out_Of_Boundaries(double latitude, double longitude)
    {
        /*
         * Проверка границ широты.
         */
        var sessionCookie = await GetSessionCookie();
        var requestData = $"title=Favorite Spot&lat={latitude}&lon={longitude}";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Theory]
    [InlineData(45.64, 180.00001)]
    [InlineData(45.64, -180.00001)]
    [InlineData(45.64, 1000.00001)]
    [InlineData(45.64, -2000.00001)]
    [InlineData(0, double.PositiveInfinity)] 
    public async Task API_Should_Return_Bad_Request_If_Longitude_Is_Out_Of_Boundaries(double latitude, double longitude)
    {
        /*
         * Проверка границ долготы.
         */
        var sessionCookie = await GetSessionCookie();
        var requestData = $"title=Favorite Spot&lat={latitude}&lon={longitude}";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task API_Should_Allow_To_Create_Spots_With_Coordinate_Zero()
    {
        /*
         * Проверка возможности обнуления координат долготы и широты.
         * Тест не проходит проверку и вызывается Internal Server Error (500).
         */
        var sessionCookie = await GetSessionCookie();
        const string requestData = "title=Favorite Spot&lat=0&lon=0";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var responseContent = JsonConvert.DeserializeObject<FavoriteResponse>(responseString);
        
        Assert.Equal(0, responseContent?.Lat);
        Assert.Equal(0, responseContent?.Lon);
        
        _testOutputHelper.WriteLine($"Response: {responseString}");
    }
    
    [Fact]
    public async Task API_Should_Not_Allow_To_Create_Spots_With_NaN_Coordinates()
    {
        /*
         * Проверка отсутствия возможности передать NaN-координаты долготы и широты.
         * Тест не проходит проверку.
         */
        var sessionCookie = await GetSessionCookie();
        const string requestData = "title=Favorite Spot&lat=NaN&lon=NaN";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Theory]
    [InlineData("BLUE")]
    [InlineData("GREEN")]
    [InlineData("RED")]
    [InlineData("YELLOW")]
    public async Task API_Should_Return_Json_With_Color_Correctly(string color)
    {
        /*
         * Проверка возможности передать цвет метки на сервер в формате по спецификации.
         */
        var sessionCookie = await GetSessionCookie();
        var requestData = $"title=Colored Location&lat=55.7558&lon=37.6173&color={color}";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stringContent = await response.Content.ReadAsStringAsync();
        var responseContent = JsonConvert.DeserializeObject<FavoriteResponse>(stringContent);

        Assert.NotNull(responseContent?.Color);
        Assert.Equal(color, responseContent?.Color);
        
        _testOutputHelper.WriteLine($"Response: {stringContent}");
    }
    
    [Theory]
    [InlineData("blue")]
    [InlineData("green")]
    [InlineData("red")]
    [InlineData("yellow")]
    [InlineData("#FF0000")]
    [InlineData("rgb(255,0,0)")]
    [InlineData("invalid-color")]
    public async Task API_Should_Not_Allow_Usage_Of_Unspecified_Color_Format(string color)
    {
        /*
         * Проверка отсутствия возможности передать цвет метки на сервер в неправильном формате.
         * Тест не проходит проверку (можно передавать название цвета метки с маленькой буквы, хотя в документе указан
         * только капс).
         */
        var sessionCookie = await GetSessionCookie();
        var requestData = $"title=Colored Location&lat=55.7558&lon=37.6173&color={color}";

        var content = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");

        request.Content = content;
        request.Headers.Add("Cookie", sessionCookie.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task API_Should_Increment_Id_Of_Favorite_Places()
    {
        /*
         * Проверка того факта, что ID отмечаемых мест увеличиваются по мере увеличения количества мест.
         */
        var sessionCookie = await GetSessionCookie();
        
        const string firstRequestData = "title=First Spot&lat=25.25&lon=25.25&color=RED";
        const string secondRequestData = "title=Second Spot&lat=60.60&lon=60.60";

        var firstContent = new StringContent(firstRequestData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var secondContent = new StringContent(secondRequestData, Encoding.UTF8, "application/x-www-form-urlencoded");

        var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");
        firstRequest.Content = firstContent;
        firstRequest.Headers.Add("Cookie", sessionCookie.ToString());

        var firstResponse = await _client.SendAsync(firstRequest);
        firstResponse.EnsureSuccessStatusCode();
        
        var firstContentString = await firstResponse.Content.ReadAsStringAsync();
        var firstData = JsonConvert.DeserializeObject<FavoriteResponse>(firstContentString);

        var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/favorites");
        secondRequest.Content = secondContent;
        secondRequest.Headers.Add("Cookie", sessionCookie.ToString());

        var secondResponse = await _client.SendAsync(secondRequest);
        secondResponse.EnsureSuccessStatusCode();

        var secondContentString = await secondResponse.Content.ReadAsStringAsync();
        var secondData = JsonConvert.DeserializeObject<FavoriteResponse>(secondContentString);
        
        Assert.True(secondData?.Id > firstData?.Id);
        
        _testOutputHelper.WriteLine($"First Response: {firstContentString}");
        _testOutputHelper.WriteLine($"Second Response: {secondContentString}");
    }
}

public class FavoriteResponse
{
    public FavoriteResponse(long id, string? title, double lat, double lon, string? color)
    {
        Id = id;
        Title = title;
        Lat = lat;
        Lon = lon;
        Color = color;
    }

    public long Id { get; }
    public string? Title { get; }
    public double Lat { get; }
    public double Lon { get; }
    public string? Color { get; }
    public string? Created_At { get; }
}