﻿using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Meltcaster.BlockEntities;

namespace Meltcaster.Gui
{
    internal class GuiDialogBlockEntityMeltcaster : GuiDialogBlockEntity
    {       
        string currentOutputText;

        long lastRedrawMs;
        EnumPosFlag screenPos;

        protected override double FloatyDialogPosition => 0.6;
        protected override double FloatyDialogAlign => 0.8;

        public override double DrawOrder => 0.2;
        public bool IsTemporal => Attributes.GetBool("isTemporal", false);
        public GuiDialogBlockEntityMeltcaster(string dlgTitle, InventoryBase Inventory, BlockPos bePos, SyncedTreeAttribute tree, ICoreClientAPI capi) : base(dlgTitle, Inventory, bePos, capi)
        {
            if (IsDuplicate) return;
            tree.OnModified.Add(new TreeModifiedListener() { listener = OnAttributesModified });
            Attributes = tree;
        }

        private void OnInventorySlotModified(int slotid)
        {
            capi.Event.EnqueueMainThreadTask(SetupDialog, "setupmeltcasterdlg");
        }

        void SetupDialog()
        {
            ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null && hoveredSlot.Inventory?.InventoryID != Inventory?.InventoryID)
            {
                hoveredSlot = null;
            }

            string newOutputText = Attributes.GetString("outputText", "");

            GuiElementDynamicText outputTextElem;

            if (SingleComposer != null)
            {
                outputTextElem = SingleComposer.GetDynamicText("outputText");
                outputTextElem.Font.WithFontSize(16);
                outputTextElem.SetNewText(newOutputText, true);
                SingleComposer.GetCustomDraw("symbolDrawer").Redraw();

                currentOutputText = newOutputText;

                outputTextElem.Bounds.fixedOffsetY = 0;

                outputTextElem.Bounds.CalcWorldBounds();

                return;
            }

            currentOutputText = newOutputText;

            ElementBounds stoveBounds = ElementBounds.Fixed(0, 0, 210, 200);

            double top = 35;

            ElementBounds inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, top, 1, 1);
            ElementBounds fuelSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 110 + top, 1, 1);
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 153, top, 2, 2).WithFixedSize(110, 110);

            ElementBounds outputTextBounds =
                currentOutputText.Length > 0 
                ? ElementBounds.Fixed(0, stoveBounds.fixedHeight + 10, 210, 75)
                : ElementBounds.Fixed(0, 0, 0, 0);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(stoveBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithFixedAlignmentOffset(IsRight(screenPos) ? -GuiStyle.DialogToScreenPadding : GuiStyle.DialogToScreenPadding, 0)
                .WithAlignment(IsRight(screenPos) ? EnumDialogArea.RightMiddle : EnumDialogArea.LeftMiddle);

            if (!capi.Settings.Bool["immersiveMouseMode"])
            {
                dialogBounds.fixedOffsetY += (stoveBounds.fixedHeight + 65) * YOffsetMul(screenPos);
                dialogBounds.fixedOffsetX += (stoveBounds.fixedWidth + 10) * XOffsetMul(screenPos);
            }

            SingleComposer = capi.Gui
                .CreateCompo("blockentitystove" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddDynamicCustomDraw(stoveBounds, OnBgDraw, "symbolDrawer")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 0 }, fuelSlotBounds, "fuelslot")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), fuelSlotBounds.RightCopy(17, 16).WithFixedSize(60, 30), "fueltemp")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 1 }, inputSlotBounds, "oreslot")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), inputSlotBounds.RightCopy(23, 16).WithFixedSize(60, 30), "oretemp")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 2, new int[] { 2, 3, 4, 5 }, outputSlotBounds, "outputslot")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), outputTextBounds, "outputText")
                .EndChildElements()
                .Compose();

            lastRedrawMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
            }

            outputTextElem = SingleComposer.GetDynamicText("outputText");
            outputTextElem.Font.WithFontSize(16);
            outputTextElem.SetNewText(currentOutputText, true);
            outputTextElem.Bounds.fixedOffsetY = 0;
            outputTextElem.Bounds.CalcWorldBounds();
        }

        private void OnAttributesModified()
        {
            if (!IsOpened()) return;

            float ftemp = Attributes.GetFloat("furnaceTemperature");
            float otemp = Attributes.GetFloat("oreTemperature");

            string fuelTemp = ftemp.ToString("#");
            string oreTemp = otemp.ToString("#");

            fuelTemp += fuelTemp.Length > 0 ? "°C" : "";
            oreTemp += oreTemp.Length > 0 ? "°C" : "";

            if (ftemp > 0 && ftemp <= 20) fuelTemp = Lang.Get("Cold");
            if (otemp > 0 && otemp <= 20) oreTemp = Lang.Get("Cold");

            SingleComposer.GetDynamicText("fueltemp").SetNewText(fuelTemp);
            SingleComposer.GetDynamicText("oretemp").SetNewText(oreTemp);

            if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
            {
                if (SingleComposer != null) SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
                lastRedrawMs = capi.ElapsedMilliseconds;
            }
        }

        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            double top = 35;

            // 1. Fire
            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(5), GuiElement.scaled(53 + top));
            m.Scale(GuiElement.scaled(0.25), GuiElement.scaled(0.25));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawFlame(ctx);
            double dy = 210 - 210 * (Attributes.GetFloat("fuelBurnTime", 0) / Attributes.GetFloat("maxFuelBurnTime", 1));            
            ctx.Rectangle(0, dy, 200, 210 - dy);
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, GuiElement.scaled(250), 0, 0);

            //temporal gradient
            //gradient.AddColorStop(0, new Color(0.08, 0.48, 0.5, 1));
            //gradient.AddColorStop(1, new Color(0.32, 1, 0.86, 1));
            gradient.AddColorStop(0, new Color(1, 1, 0, 1));
            gradient.AddColorStop(1, new Color(1, 0, 0, 1));

            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawFlame(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();


            // 2. Arrow Right
            ctx.Save();
            m = ctx.Matrix;
            m.Translate(GuiElement.scaled(63), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            double cookingRel = Attributes.GetFloat("oreCookingTime") / Attributes.GetFloat("maxOreCookingTime", 1);
            ctx.Rectangle(5, 0, 125 * cookingRel, 100);
            ctx.Clip();
            gradient = new LinearGradient(0, 0, 200, 0);
            gradient.AddColorStop(0, new Color(0, 0.4, 0, 1));
            gradient.AddColorStop(1, new Color(0.2, 0.6, 0.2, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }

        private void SendInvPacket(object packet)
        {
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, packet);
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnInventorySlotModified;

            screenPos = GetFreePos("smallblockgui");
            OccupyPos("smallblockgui", screenPos);
            SetupDialog();
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnInventorySlotModified;

            SingleComposer.GetSlotGrid("fuelslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("oreslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputslot")?.OnGuiClosed(capi);

            base.OnGuiClosed();

            FreePos("smallblockgui", screenPos);
        }
    }
}
