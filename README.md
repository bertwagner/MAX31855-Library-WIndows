# MAX31855-Library-Windows
A library for using the MAX31855 thermocouple amplifier with Windows IoT C# on a Raspberry Pi 2/3.

Based on Adafruit's Arduino library here: https://github.com/adafruit/Adafruit-MAX31855-library

## How to use

    private BackgroundTaskDeferral deferral;
    public async void Run(IBackgroundTaskInstance taskInstance)
    {
        deferral = taskInstance.GetDeferral();
        
        MAX31855 m = new MAX31855();
        while (1 == 1)
        {
            double celsius = m.GetProbeTemperatureDataCelsius();
            double internalC = m.GetInternalTemperatureDataCelcius();
            double fahrenheit = m.GetProbeTemperatureDataFahrenheit();
            double internalF = m.GetInternalTemperatureDataFahrenheit();
        
            Debug.WriteLine("{0}*F", fahrenheit);
            await Task.Delay(500);
        }
    }
