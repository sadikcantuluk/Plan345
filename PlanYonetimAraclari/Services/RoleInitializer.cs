using Microsoft.AspNetCore.Identity;
using PlanYonetimAraclari.Models;
using System;
using System.Threading.Tasks;

namespace PlanYonetimAraclari.Services
{
    public static class RoleInitializer
    {
        public static async Task InitializeAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            // Rolleri oluştur
            string[] roleNames = { "Admin", "User" };
            
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Test Kullanıcıları Oluştur
            await EnsureUserExists(userManager, "admin@plan345.com", "Admin123!", "Admin", "Kullanıcı", "Admin");
            await EnsureUserExists(userManager, "user@plan345.com", "User123!", "Test", "Kullanıcı", "User");
            await EnsureUserExists(userManager, "demo@plan345.com", "Demo123!", "Demo", "Kullanıcı", "User");
            
            // Test amaçlı olarak belirtilen e-posta adresini ekleyelim
            await EnsureUserExists(userManager, "sadikcantuluk@gmail.com", "Test123!", "Sadık", "Tuluk", "User");
        }
        
        private static async Task EnsureUserExists(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string firstName,
            string lastName,
            string role)
        {
            // E-posta adresine göre kullanıcıyı ara
            var user = await userManager.FindByEmailAsync(email);
            
            // Kullanıcı yoksa oluştur
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true,
                    CreatedDate = DateTime.Now,
                    ProfileImageUrl = "/images/profiles/default.jpg"
                };
                
                var result = await userManager.CreateAsync(user, password);
                
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
} 