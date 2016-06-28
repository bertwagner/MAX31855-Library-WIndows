class MAX31855
{
    private ThreadPoolTimer timer;
    private SpiDevice thermocouple;
    private byte[] thermocoupleData = new byte[4];

    public Temperature()
    {
        InitSpi();
        timer = ThreadPoolTimer.CreatePeriodicTimer(GetThermocoupleData, TimeSpan.FromMilliseconds(1000));
    }

    public double GetProbeTemperatureDataCelsius()
    {
        uint data = BitConverter.ToUInt32(thermocoupleData, 0);

        // Any of the last 3 bits are 1s - error with circuit
        if ((data & 0x7) != 0)
        {
            throw new Exception("Error with MAX31855 device");
        }

        // Negative value, drop the lower 18 bits and explicitly extend sign bits.
        if ((data & 0x80000000) != 0)
        {
            data = ((data >> 18) & 0x00003FFFF) | 0xFFFFC000;
        }
        // Positive value, just drop the lower 18 bits.
        else
        {
            data >>= 18;
        }

        double celsius = data;

        // LSB = 0.25 degrees C 
        celsius *= 0.25;
        return celsius;
    }

    public double GetInternalTemperatureDataCelcius()
    {
        uint data = BitConverter.ToUInt32(thermocoupleData, 0);

        // Ignore bottom 4 digits
        data >>= 4;

        // pull the bottom 11 bits off 
        double celsius = data & 0x7FF;

        // check sign bit! 
        if ((data & 0x800) == 1)
        {
            // Convert to negative value by extending sign and casting to signed type. 
            celsius = 0xF800 | (data & 0x7FF);
        }

        //LSB = .0625 degrees C
        celsius *= .0625;
        return celsius;
    }

    public double GetProbeTemperatureDataFahrenheit()
    {
        return ConvertCelsiusToFahrenheit(GetProbeTemperatureDataCelsius());
    }

    public double GetInternalTemperatureDataFahrenheit()
    {
        return ConvertCelsiusToFahrenheit(GetInternalTemperatureDataCelcius());
    }

    // calibration code https://learn.adafruit.com/calibrating-sensors/maxim-31855-linearization
    public double GetCorrectedCelius()
    {
        // MAX31855 thermocouple voltage reading in mV
        double thermocoupleVoltage = (GetProbeTemperatureDataCelsius() - GetInternalTemperatureDataCelcius()) * 0.041276;

        // MAX31855 cold junction voltage reading in mV
        double coldJunctionTemperature = GetInternalTemperatureDataCelcius();
        double coldJunctionVoltage = -0.176004136860E-01 +
           0.389212049750E-01 * coldJunctionTemperature +
           0.185587700320E-04 * Math.Pow(coldJunctionTemperature, 2.0) +
           -0.994575928740E-07 * Math.Pow(coldJunctionTemperature, 3.0) +
           0.318409457190E-09 * Math.Pow(coldJunctionTemperature, 4.0) +
           -0.560728448890E-12 * Math.Pow(coldJunctionTemperature, 5.0) +
           0.560750590590E-15 * Math.Pow(coldJunctionTemperature, 6.0) +
           -0.320207200030E-18 * Math.Pow(coldJunctionTemperature, 7.0) +
           0.971511471520E-22 * Math.Pow(coldJunctionTemperature, 8.0) +
           -0.121047212750E-25 * Math.Pow(coldJunctionTemperature, 9.0) +
           0.118597600000E+00 * Math.Exp(-0.118343200000E-03 *
                                Math.Pow((coldJunctionTemperature - 0.126968600000E+03), 2.0)
                             );


        // cold junction voltage + thermocouple voltage         
        double voltageSum = thermocoupleVoltage + coldJunctionVoltage;

        // calculate corrected temperature reading based on coefficients for 3 different ranges   
        double b0, b1, b2, b3, b4, b5, b6, b7, b8, b9;
        if (thermocoupleVoltage < 0)
        {
            b0 = 0.0000000E+00;
            b1 = 2.5173462E+01;
            b2 = -1.1662878E+00;
            b3 = -1.0833638E+00;
            b4 = -8.9773540E-01;
            b5 = -3.7342377E-01;
            b6 = -8.6632643E-02;
            b7 = -1.0450598E-02;
            b8 = -5.1920577E-04;
            b9 = 0.0000000E+00;
        }
        else if (thermocoupleVoltage < 20.644)
        {
            b0 = 0.000000E+00;
            b1 = 2.508355E+01;
            b2 = 7.860106E-02;
            b3 = -2.503131E-01;
            b4 = 8.315270E-02;
            b5 = -1.228034E-02;
            b6 = 9.804036E-04;
            b7 = -4.413030E-05;
            b8 = 1.057734E-06;
            b9 = -1.052755E-08;
        }
        else if (thermocoupleVoltage < 54.886)
        {
            b0 = -1.318058E+02;
            b1 = 4.830222E+01;
            b2 = -1.646031E+00;
            b3 = 5.464731E-02;
            b4 = -9.650715E-04;
            b5 = 8.802193E-06;
            b6 = -3.110810E-08;
            b7 = 0.000000E+00;
            b8 = 0.000000E+00;
            b9 = 0.000000E+00;
        }
        else
        {
            // TODO: handle error - out of range
            return 0;
        }

        return b0 +
           b1 * voltageSum +
           b2 * Math.Pow(voltageSum, 2.0) +
           b3 * Math.Pow(voltageSum, 3.0) +
           b4 * Math.Pow(voltageSum, 4.0) +
           b5 * Math.Pow(voltageSum, 5.0) +
           b6 * Math.Pow(voltageSum, 6.0) +
           b7 * Math.Pow(voltageSum, 7.0) +
           b8 * Math.Pow(voltageSum, 8.0) +
           b9 * Math.Pow(voltageSum, 9.0);


    }

    public double GetCorrectedFahrenheit()
    {
        return ConvertCelsiusToFahrenheit(GetCorrectedCelius());
    }

    private async void InitSpi()
    {
        try
        {
            var settings = new SpiConnectionSettings(0);
            settings.ClockFrequency = 5000000;
            settings.Mode = SpiMode.Mode0;

            string spiAqs = SpiDevice.GetDeviceSelector("SPI0");
            var deviceInfo = await DeviceInformation.FindAllAsync(spiAqs);
            thermocouple = await SpiDevice.FromIdAsync(deviceInfo[0].Id, settings);
        }

        catch (Exception ex)
        {
            throw new Exception("SPI Initialization Failed", ex);
        }
    }

    private void GetThermocoupleData(ThreadPoolTimer timer)
    {
        byte[] readBuffer = new byte[4];
        thermocouple.Read(readBuffer);

        //Data from the sensor is big endian.  We need to convert to little endian.
        Array.Reverse(readBuffer);

        thermocoupleData = readBuffer;
    }

    private double ConvertCelsiusToFahrenheit(double c)
    {
        c *= 9.0;
        c /= 5.0;
        c += 32;
        return c;
    }

    
}
