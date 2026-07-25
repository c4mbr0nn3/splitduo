using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Categories.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.PaymentModes.Dto;

namespace SplitDuo.Tests.Integration;

public class CategoriesAndPaymentModesTests : IntegrationTest
{
    public CategoriesAndPaymentModesTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Categories

    [Fact]
    public async Task GetCategories_Authenticated_ReturnsAll11Categories()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/categories", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<CategoryDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(11, body.Data!.Count);
        Assert.Equal(1, body.Data[0].Id);
        Assert.Equal("Other", body.Data[0].Name);
        Assert.Contains(body.Data, c => c.Name == "Groceries" && c.Id == 2);
        Assert.Contains(body.Data, c => c.Name == "Dining" && c.Id == 11);
    }

    [Fact]
    public async Task GetCategories_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/api/v1/categories", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region PaymentModes

    [Fact]
    public async Task GetPaymentModes_Authenticated_ReturnsAll6PaymentModes()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/payment-modes", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<PaymentModeDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(6, body.Data!.Count);
        Assert.Equal(1, body.Data[0].Id);
        Assert.Equal("Other", body.Data[0].Name);
        // Display names differ from enum names for two values
        Assert.Contains(body.Data, p => p.Name == "Online Services" && p.Id == 5);
        Assert.Contains(body.Data, p => p.Name == "Ticket Restaurant" && p.Id == 6);
    }

    [Fact]
    public async Task GetPaymentModes_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/api/v1/payment-modes", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}