using System;
using QuickStackStore.API;
using UnityEngine;

namespace QuickStackStore.IContainers;

public class kgDrawer(ItemDrawers_API.Drawer _drawer) : ContainerWrapper
{
    public bool ContainsItem(string prefab)
    {
        return _drawer.Prefab == prefab;
    }

    public int GetAmount()
    {
        return _drawer.Amount;
    }
    
    public void AddItem(string prefab, int amount)
    {
        _drawer.Add(amount);
    }

    public Vector3 GetPosition() => _drawer.Position;
    
    public static kgDrawer Create(ItemDrawers_API.Drawer drawer) => new(drawer);
}