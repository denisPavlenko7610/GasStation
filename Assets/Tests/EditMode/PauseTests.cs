using GasStation.Bridge;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class PauseTests
    {
        private float _timeScale;
        private int _speed;

        [SetUp]
        public void Remember()
        {
            _timeScale = Time.timeScale;
            _speed = GameSettings.GameSpeed;
        }

        [TearDown]
        public void Restore()
        {
            GamePause.SetMenuOpen(false);
            GameSettings.GameSpeed = _speed;
            Time.timeScale = _timeScale;
        }

        [Test]
        public void OpenMenu_StopsTime_AndClosingRestoresGameSpeed()
        {
            GamePause.SetGameSpeed(2);
            GamePause.SetMenuOpen(true);
            Assert.AreEqual(0f, Time.timeScale);

            GamePause.SetMenuOpen(false);
            Assert.AreEqual(2f, Time.timeScale);
        }

        [Test]
        public void GameSpeed_IsClampedToThree()
        {
            GamePause.SetGameSpeed(10);
            Assert.AreEqual(3, GameSettings.GameSpeed);
            GamePause.SetGameSpeed(0);
            Assert.AreEqual(1, GameSettings.GameSpeed);
        }
    }
}
