#include <Wire.h>
#include "MAX30105.h"

MAX30105 particleSensor;

// ===== ECG 算心率 =====
int ecg = 512;
int ecgThreshold = 550;
bool aboveThreshold = false;
long lastPeakTime = 0;
int bpm = 0;
const int RATE_SIZE = 4;
int rates[RATE_SIZE];
int rateSpot = 0;
int bpmAvg = 0;

unsigned long lastVitalTime = 0;
int spo2Display = 0;

void setup() {
  Serial.begin(115200);

  if (!particleSensor.begin(Wire, I2C_SPEED_FAST)) {
    Serial.println("找不到 MAX30102");
    while (1);
  }
  particleSensor.setup();                      // 預設設定，LED 會亮
  particleSensor.setPulseAmplitudeRed(0x0A);
  particleSensor.setPulseAmplitudeIR(0x0A);

  pinMode(10, INPUT);
  pinMode(11, INPUT);
}

void loop() {
  // ===== ECG 讀取 + 心率 =====
  if ((digitalRead(10) == 1) || (digitalRead(11) == 1)) {
    // 貼片脫落，維持上一個值
  } else {
    ecg = analogRead(A0);
  }
  Serial.print("E:");
  Serial.println(ecg);

  if (ecg > ecgThreshold && !aboveThreshold) {
    aboveThreshold = true;
    long now = millis();
    long interval = now - lastPeakTime;
    if (interval > 300 && interval < 2000) {
      bpm = 60000 / interval;
      rates[rateSpot++] = bpm;
      rateSpot %= RATE_SIZE;
      bpmAvg = 0;
      for (int i = 0; i < RATE_SIZE; i++) bpmAvg += rates[i];
      bpmAvg /= RATE_SIZE;
    }
    lastPeakTime = now;
  }
  if (ecg < ecgThreshold - 50) aboveThreshold = false;

  // ===== 簡化血氧：用紅光/紅外光比值估算 =====
  long ir = particleSensor.getIR();
  long red = particleSensor.getRed();

  if (ir < 20000) {
    spo2Display = 0;   // 沒手指
  } else {
    // 用紅光與紅外光的比值粗估血氧
    // 比值越低，血氧越高（正常人約 95~99）
    float ratio = (float)red / (float)ir;
// 你的比值約 1.0~1.2，對應血氧 95~99
    int est = (int)(125 - 25 * ratio);
    if (est > 100) est = 100;
    if (est < 90) est = 90;
    spo2Display = est;
  }

  // ===== 每秒送一次 =====
  if (millis() - lastVitalTime >= 1000) {
    lastVitalTime = millis();
    Serial.print("V:");
    Serial.print(bpmAvg);
    Serial.print(",");
    Serial.println(spo2Display);
  }
}