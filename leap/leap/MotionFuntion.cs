﻿using Leap;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace leap
{
    class MotionFuntion     // 모션 인식함수
    {
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, int dwExtraInfo);
        [DllImport("User32.dll")]
        public static extern void keybd_event(uint vk, uint scan, uint flags, uint extraInfo);

        public static bool setPTmode = false;
        double pointX = 0.0, pointY = 0.0;
        double tmp_palm = 0.0;
        double tmp_roll = 0.0;
        bool sensitiveMoving = false;
        bool pinch_ready = false;
        bool drag_ready = false;
        bool pull_ready = false;
        int roll_ready = 0;
        int clickCount = 0;
        private int screenWidth, screenHeight;
        private int frameCount = 0;
        bool clap_ready = false;

        public MotionFuntion(int screenWidth, int screenHeight)
        {
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;
        }

        public void grab(Frame frame)
        {
            if (frame.Hands.Count == 2)
            {
                Hand hand = frame.Hands[1];
                if (hand.GrabStrength == (int)MotionEnum.GRAB.Grab && !sensitiveMoving)
                {
                    sensitiveMoving = true;
                    pointX = Cursor.Position.X;
                    pointY = Cursor.Position.Y;
                    //Console.WriteLine("세부동작 감지 : " + pointX + ", " + pointY);
                }
                else if (hand.GrabStrength == (int)MotionEnum.GRAB.UnGrab && sensitiveMoving)
                {
                    sensitiveMoving = false;
                }
            }
            else if (sensitiveMoving)
            {
                sensitiveMoving = false;
            }
            else
            {
                Hand hand = frame.Hands[0];
                if (hand.GrabStrength > 0.95 && roll_ready == 0)
                {
                    roll_ready = 1;
                    tmp_roll = hand.PalmNormal.Roll;
                }
                else if (hand.GrabStrength > 0.95 && roll_ready == 1)
                {
                    if ((hand.PalmNormal.Roll - tmp_roll) > 1)
                    {
                        roll_ready = 2;
                        keybd_event((byte)Keys.Left, 0x00, 0x00, 0);
                    }
                    else if ((hand.PalmNormal.Roll - tmp_roll) < -1)
                    {
                        roll_ready = 2;
                        keybd_event((byte)Keys.Right, 0x00, 0x00, 0);
                    }
                }
                else if (hand.GrabStrength < 0.05 && roll_ready == 2)
                    roll_ready = 0;
            }
        }

        public void pinch(Frame frame)
        {
            Hand hand = frame.Hands[0];
            if (hand.PinchStrength == 1 && !pinch_ready)
                pinch_ready = true;
            else if (hand.PinchStrength > 0.95 && pinch_ready && !pull_ready)
            {
                frameCount++;
                if (!drag_ready && frameCount == 10)
                {
                    mouse_event((uint)MotionEnum.MOUSE.MouseLeftDown, 0, 0, 0, 0);
                    drag_ready = true;
                    sensitiveMoving = true;
                    pointX = Cursor.Position.X;
                    pointY = Cursor.Position.Y;
                }
                else if (frameCount > 40)
                    drag_ready = false;
            }
            else if (hand.PinchStrength < 0.35 && pinch_ready)
            {
                mouse_event((uint)MotionEnum.MOUSE.MouseLeftUp, 0, 0, 0, 0);
                if (frameCount < 60)
                    clickCount++;
                if (clickCount == 2)
                {
                    clickCount = 0;
                    mouse_event((uint)MotionEnum.MOUSE.MouseLeftDown, 0, 0, 0, 0);
                    mouse_event((uint)MotionEnum.MOUSE.MouseLeftUp, 0, 0, 0, 0);
                }
                pinch_ready = false;
                sensitiveMoving = false;
                drag_ready = false;
                frameCount = 0;
            }
        }

        public void setMouseCursor(Frame frame)
        {
            if (!pull_ready && !drag_ready)
            {
                if (!sensitiveMoving)
                {
                    //Console.WriteLine("{0}, {1}, {2}, {3}", Cursor.Position.X, Cursor.Position.Y, frame.Hands[0].PalmPosition.x, frame.Hands[0].PalmPosition.y);
                    double mousePointX = (frame.Hands[0].PalmPosition.x + 300) / 600 * screenWidth;
                    double mousePointY = (1 - (frame.Hands[0].PalmPosition.y - 200) / 300) * screenHeight;
                    SetCursorPos((int)mousePointX, (int)mousePointY);
                    //Console.WriteLine("일반동작 감지 : " + (int)mousePointX + ", " + (int)mousePointY + " = " + frame.Hands.Count);
                }
                else
                {
                    double mousePointX = (frame.Hands[0].PalmPosition.x + 300) / 600 * screenWidth;
                    double mousePointY = (1 - (frame.Hands[0].PalmPosition.y - 200) / 300) * screenHeight;
                    double scaledX = (mousePointX - pointX) / 300;
                    double scaledY = (mousePointY - pointY) / 300;
                    //if (Math.Abs(scaledX) > 1)
                    pointX += scaledX;
                    //if (Math.Abs(scaledY) > 1)
                    pointY += scaledY;
                    SetCursorPos((int)pointX, (int)pointY);
                    //Console.WriteLine("세부동작 감지 : " + pointX + ", " + pointY + " = " + frame.Hands.Count);
                }
            }
        }

        public void wheel(Leap.Frame frame)
        {
            if (roll_ready == 0 && !setPTmode)
            {
                if (frame.Hands[0].PalmNormal.z < -0.65)
                {
                    // 150 = 스크롤 업
                    mouse_event((uint)MotionEnum.MOUSE.MouseWheel, 0, 0, 150, 0);
                }
                else if (frame.Hands[0].PalmNormal.z > 0.35)
                {
                    // -150 = 스크롤 다운
                    mouse_event((uint)MotionEnum.MOUSE.MouseWheel, 0, 0, -150, 0);
                }
            }
        }

        public void grabPull(Frame frame) 
        {
            Hand hand = frame.Hands[0];
            if (hand.GrabStrength == 1 && !pull_ready)     // main hand -> grab
            {
                pull_ready = true;
                tmp_palm = hand.PalmPosition[2]; // 초기값 입력 -
            }
            else if (hand.GrabStrength > 0.95 && pull_ready)
            {
                if ((tmp_palm - hand.PalmPosition[2]) < -200)
                {
                    pull_ready = false;
                    mouse_event((uint)MotionEnum.MOUSE.MouseRightDown, 0, 0, 0, 0);
                    mouse_event((uint)MotionEnum.MOUSE.MouseRightUp, 0, 0, 0, 0);
                }
                Console.WriteLine("waiting pull ...  => " + hand.PalmPosition[2] + " : remain => " + (tmp_palm - hand.PalmPosition[2]));
            }
            else if (hand.GrabStrength == 0 && pull_ready)
            {
                pull_ready = false;
            }
        }

        public void clap(Frame frame)
        {
            if (frame.Hands.Count == 2)
            {
                Hand hand1 = frame.Hands[0];
                Hand hand2 = frame.Hands[1];
                if (hand1.GrabStrength < 0.05 && hand2.GrabStrength < 0.05)
                {

                    float dist = Math.Abs(hand1.PalmPosition.x - hand2.PalmPosition.x);
                    if (dist < 100 && dist > 10 && !clap_ready)
                    {
                        clap_ready = true;
                    }
                    else if (clap_ready && dist <= 10)
                    {
                        if (MotionFuntion.setPTmode)
                        {
                            MotionFuntion.setPTmode = false;
                            keybd_event((byte)Keys.Escape, 0x00, 0x00, 0);
                        }
                        else
                        {
                            MotionFuntion.setPTmode = true;
                            keybd_event((byte)Keys.F5, 0x00, 0x00, 0);
                        }
                        clap_ready = false;
                    }
                }
            }
        }
        //public void pinch(Frame frame)    // 모션추가 베이직
        //{
        //    Hand hand = frame.Hands[0];
        //}
    }
}
