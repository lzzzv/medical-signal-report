#include <Wire.h>
#include <Adafruit_SSD1306.h>
#include "Adafruit_AM2320.h"
#include <Adafruit_MLX90614.h>
#include <VL53L0X.h>
#include <SoftwareSerial.h>

#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, -1);

SoftwareSerial BT(10, 11);
Adafruit_AM2320 am2320 = Adafruit_AM2320();
Adafruit_MLX90614 mlx = Adafruit_MLX90614();
VL53L0X distSensor;

unsigned long lastTime = 0;

void setup() {
  Serial.begin(9600);
  BT.begin(9600);
  Wire.begin();
  
  // 屏幕初始化
  display.begin(SSD1306_SWITCHCAPVCC, 0x3C);
  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);
  display.setCursor(0, 0);
  display.println("Initializing...");
  display.display();

  am2320.begin();
  mlx.begin();
  distSensor.init();
  distSensor.setTimeout(500);
  pinMode(2, INPUT);
  
  delay(1000);
}

void loop() {
  if (millis() - lastTime >= 1000) {
    lastTime = millis();

    float temp = am2320.readTemperature();
    float humidity = am2320.readHumidity();
    float objTemp = mlx.readObjectTempC();
    int distance = distSensor.readRangeSingleMillimeters();
    int motion = digitalRead(2);

    // 蓝牙传数据
    String data = "ENV:" + String(temp, 1) + "," +
                  String(humidity, 1) + "," +
                  String(objTemp, 1) + "," +
                  String(distance) + "," +
                  String(motion);

    Serial.println(data);
    BT.println(data);
    
    // 屏幕显示
    display.clearDisplay();
    display.setTextSize(1);
    display.setCursor(0, 0);
    display.print("T:"); display.println(String(temp, 1) + "C");
    display.print("H:"); display.println(String(humidity, 1) + "%");
    display.print("OT:"); display.println(String(objTemp, 1) + "C");
    display.print("D:"); display.println(String(distance) + "mm");
    display.print("Motion:"); display.println(motion ? "YES" : "NO");
    display.display();
  }
}