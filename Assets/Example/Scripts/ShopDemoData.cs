using System;
using System.Collections.Generic;

namespace GameFramework.Examples
{
    public enum ShopDemoCategory { All, Supplies, Equipment, Materials }
    public enum ShopPurchaseStatus { Success, NotFound, SoldOut, InsufficientFunds }

    public sealed class ShopDemoProduct
    {
        public int Id;
        public string Name;
        public int IconIndex;
        public int Price;
        public int Stock;
        public ShopDemoCategory Category;
    }

    public sealed class ShopPurchaseResult
    {
        public ShopPurchaseStatus Status;
        public int ProductId;
        public int Balance;
    }

    public sealed class ShopDemoChanged { }

    public sealed class ShopDemoModel : AbstractModel
    {
        internal readonly List<ShopDemoProduct> Products = new List<ShopDemoProduct>();
        public IReadOnlyList<ShopDemoProduct> Snapshot => Products;
        public int Balance { get; internal set; } = 500;

        protected override void OnInit()
        {
            var names = new[] { "生命药剂", "魔力药剂", "铁剑", "旅行补给", "古老徽记", "骑士护甲" };
            for (int i = 0; i < 36; i++)
                Products.Add(new ShopDemoProduct {
                    Id = i + 1, Name = names[i % names.Length] + " " + (i + 1),
                    IconIndex = i % names.Length, Price = 25 + i * 12, Stock = i == 5 ? 0 : 2 + i % 6,
                    Category = i % 6 == 2 || i % 6 == 5 ? ShopDemoCategory.Equipment :
                        i % 6 == 4 ? ShopDemoCategory.Materials : ShopDemoCategory.Supplies
                });
        }
    }

    // 联机实现保留相同的请求和结果契约，在服务器确认后更新 Model 并回调。
    public interface IShopDemoSystem : ISystem
    {
        void Purchase(int productId, Action<ShopPurchaseResult> completed);
    }

    public sealed class LocalShopDemoSystem : AbstractISystem, IShopDemoSystem
    {
        protected override void OnInit() { }

        public void Purchase(int productId, Action<ShopPurchaseResult> completed)
        {
            var model = this.GetModel<ShopDemoModel>();
            var product = model.Products.Find(value => value.Id == productId);
            var status = product == null ? ShopPurchaseStatus.NotFound :
                product.Stock <= 0 ? ShopPurchaseStatus.SoldOut :
                model.Balance < product.Price ? ShopPurchaseStatus.InsufficientFunds : ShopPurchaseStatus.Success;
            if (status == ShopPurchaseStatus.Success)
            {
                model.Balance -= product.Price;
                product.Stock--;
                this.SendEvent<ShopDemoChanged>();
            }
            completed?.Invoke(new ShopPurchaseResult { Status = status, ProductId = productId, Balance = model.Balance });
        }
    }
}
