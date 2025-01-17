﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyRestaurantProject.Authorization;
using MyRestaurantProject.Entities;
using MyRestaurantProject.Exceptions;
using MyRestaurantProject.Models;
using MyRestaurantProject.Models.Enums;

namespace MyRestaurantProject.Services
{
    public interface IRestaurantService
    {
        PagedResult<RestaurantDto> GetAll(RestaurantQuery queryParams);
        RestaurantDto Get(int id);
        int CreateRestaurant(CreateRestaurantDto createDto);
        void Delete(int id);
        void UpdateRestaurant(UpdateRestaurantDto updateDto, int id);
    }

    public class RestaurantService : IRestaurantService
    {
        private readonly IMapper _mapper;
        private readonly RestaurantDbContext _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<RestaurantService> _logger;
        private readonly IUserContextService _userContext;

        public RestaurantService(IMapper mapper,
            RestaurantDbContext dbContext,
            IAuthorizationService authorizationService,
            ILogger<RestaurantService> logger,
            IUserContextService userContext)
        {
            _mapper = mapper;
            _dbContext = dbContext;
            _authorizationService = authorizationService;
            _logger = logger;
            _userContext = userContext;
        }
        
        public PagedResult<RestaurantDto> GetAll(RestaurantQuery queryParams)
        {
            var searchPhraseToLower = queryParams.SearchPhrase?.ToLower();

            var isResultShouldBeOrder = queryParams.SortDirection.HasValue && (queryParams.SortBy != null && !string.IsNullOrWhiteSpace(queryParams.SortBy));

            var baseQuery = _dbContext
                .Restaurants
                .Include(x => x.Address)
                .Include(x => x.Dishes)
                .Where(r => searchPhraseToLower == null ||
                            (r.Name.ToLower().Contains(searchPhraseToLower)
                             || r.Description.ToLower().Contains(searchPhraseToLower)));

            if (isResultShouldBeOrder)
            {
                var propertySelector = new Dictionary<string, Expression<Func<Restaurant, object>>>
                {
                    {nameof(Restaurant.Description), r => r.Description},
                    {nameof(Restaurant.Address), r => r.Address},
                    {nameof(Restaurant.Name), r => r.Name}
                };

                baseQuery = queryParams.SortDirection == SortDirection.ASC
                    ? baseQuery.OrderBy(propertySelector[queryParams.SortBy])
                    : baseQuery.OrderByDescending(propertySelector[queryParams.SortBy]);
            }
            
            var restaurants = baseQuery
                .Skip(queryParams.PageNumber * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToList();
            
            var restaurantsDto = _mapper.Map<List<RestaurantDto>>(restaurants);

            var result = new PagedResult<RestaurantDto>(
                items: restaurantsDto.ToList(),
                totalItems: baseQuery.Count(),
                pageSize: queryParams.PageSize,
                pageNumber: queryParams.PageNumber);
            
            return result;
        }
        
        public RestaurantDto Get(int id)
        {
            var restaurant = _dbContext
                .Restaurants
                .Where(x => x.Id == id)
                .Include(x => x.Address)
                .Include(x => x.Dishes)
                .FirstOrDefault();
            
            var restaurantDto = _mapper.Map<RestaurantDto>(restaurant);
            return restaurantDto;
        }

        public int CreateRestaurant(CreateRestaurantDto createDto)
        {
            var userId = _userContext.GetUserId;
            
            var restaurant = _mapper.Map<Restaurant>(createDto);
            restaurant.CreatedById = userId;
            _dbContext.Add(restaurant);
            _dbContext.SaveChanges();

            return restaurant.Id;
        }

        public void Delete(int id)
        {
            _logger.LogError($"Restaurant with id: {id} DELETE action invoked");
            
            var restaurant = _dbContext
                .Restaurants
                .FirstOrDefault(x => x.Id == id);

            if (restaurant is null)
                throw new NotFoundException("Restaurant not found");

            var user = _userContext.User;
            
            var authResult = _authorizationService
                .AuthorizeAsync(user, restaurant, new ResourceOperationRequirement(ResourceOperation.Delete)).Result;

            if (!authResult.Succeeded)
            {
                throw new ForbidException();
            }
            
            _dbContext.Remove(restaurant);
            _dbContext.SaveChanges();
        }

        public void UpdateRestaurant(UpdateRestaurantDto updateDto, int id)
        {
            var restaurant = _dbContext
                .Restaurants
                .FirstOrDefault(x => x.Id == id);

            if (restaurant is null)
                throw new NotFoundException("Restaurant not found");

            var user = _userContext.User; 
            
            var authResult = _authorizationService.AuthorizeAsync(user, restaurant,
                new ResourceOperationRequirement(ResourceOperation.Update)).Result;

            if (!authResult.Succeeded)
            {
                throw new ForbidException();
            }
            
            restaurant.Name = updateDto.Name;
            restaurant.Description = updateDto.Description;
            restaurant.HasDelivery = updateDto.HasDelivery;

            _dbContext.SaveChanges();
        }
    }
}