using System;
using QuickStackStore.API;
using UnityEngine;

namespace QuickStackStore.IContainers;

public class VanillaContainer(Container _container) : ContainerWrapper
{

    public Container Container => _container;
    public static VanillaContainer Create(Container container) => new(container);
}