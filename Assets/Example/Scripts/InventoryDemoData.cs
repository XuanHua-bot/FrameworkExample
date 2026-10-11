using System.Collections.Generic;

namespace GameFramework.Examples
{
    public sealed class InventoryDemoItem
    {
        public int Id;
        public string Name;
        public int IconIndex;
        public int Count;
        public bool Locked;
        public bool Consumable;
        public InventoryDemoCategory Category;
        public InventoryDemoEquipment Equipment;
    }

    public enum InventoryDemoCategory { All, Consumable, Weapon, Armor, Material }
    public enum InventoryDemoEquipment { None, Weapon, Armor }

    public sealed class InventoryDemoChanged { }

    public sealed class InventoryDemoModel : AbstractModel
    {
        internal readonly List<InventoryDemoItem> Items = new List<InventoryDemoItem>();
        public int EquippedWeaponId { get; private set; }
        public int EquippedArmorId { get; private set; }
        public IReadOnlyList<InventoryDemoItem> Snapshot => Items;
        internal void Equip(int id, InventoryDemoEquipment slot)
        {
            if (slot == InventoryDemoEquipment.Weapon) EquippedWeaponId = id;
            else EquippedArmorId = id;
        }
        protected override void OnInit() => Restore();

        internal void Restore()
        {
            Items.Clear();
            EquippedWeaponId = 0;
            EquippedArmorId = 0;
            var names = new[] { "生命药剂", "魔力药剂", "铁剑", "旅行补给", "古老徽记", "骑士护甲" };
            for (int i = 0; i < 1200; i++)
            {
                var category = i % 6 == 2 ? InventoryDemoCategory.Weapon :
                    i % 6 == 5 ? InventoryDemoCategory.Armor :
                    i % 6 == 4 ? InventoryDemoCategory.Material : InventoryDemoCategory.Consumable;
                Items.Add(new InventoryDemoItem
                {
                    Id = i + 1, Name = names[i % names.Length] + " " + (i + 1),
                    IconIndex = i % names.Length, Count = i % 5 + 1,
                    Locked = i % 7 == 0, Consumable = category == InventoryDemoCategory.Consumable,
                    Category = category,
                    Equipment = category == InventoryDemoCategory.Weapon ? InventoryDemoEquipment.Weapon :
                        category == InventoryDemoCategory.Armor ? InventoryDemoEquipment.Armor : InventoryDemoEquipment.None
                });
            }
        }
    }

    public interface IInventoryDemoSystem : ISystem
    {
        bool Use(int id);
        bool Equip(int id, InventoryDemoEquipment slot);
        void Restore();
    }

    // 将来联机实现仍接收稳定 ID，由服务器确认后再更新 Model。
    public sealed class LocalInventoryDemoSystem : AbstractISystem, IInventoryDemoSystem
    {
        protected override void OnInit() { }

        public bool Use(int id)
        {
            var items = this.GetModel<InventoryDemoModel>().Items;
            var item = items.Find(value => value.Id == id);
            if (item == null || item.Locked || !item.Consumable) return false;
            if (--item.Count == 0) items.Remove(item);
            this.SendEvent<InventoryDemoChanged>();
            return true;
        }

        public bool Equip(int id, InventoryDemoEquipment slot)
        {
            var model = this.GetModel<InventoryDemoModel>();
            var item = model.Items.Find(value => value.Id == id);
            if (item == null || item.Locked || item.Equipment != slot || slot == InventoryDemoEquipment.None) return false;
            model.Equip(id, slot);
            this.SendEvent<InventoryDemoChanged>();
            return true;
        }

        public void Restore()
        {
            this.GetModel<InventoryDemoModel>().Restore();
            this.SendEvent<InventoryDemoChanged>();
        }
    }

    public sealed class EquipInventoryDemoItem : AbstractCommand<bool>
    {
        private readonly int mId;
        private readonly InventoryDemoEquipment mSlot;
        public EquipInventoryDemoItem(int id, InventoryDemoEquipment slot) { mId = id; mSlot = slot; }
        protected override bool OnExecute() => this.GetSystem<IInventoryDemoSystem>().Equip(mId, mSlot);
    }

    public sealed class UseInventoryDemoItem : AbstractCommand<bool>
    {
        private readonly int mId;
        public UseInventoryDemoItem(int id) => mId = id;
        protected override bool OnExecute() => this.GetSystem<IInventoryDemoSystem>().Use(mId);
    }

    public sealed class RestoreInventoryDemo : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<IInventoryDemoSystem>().Restore();
    }
}
