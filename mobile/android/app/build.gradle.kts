plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

android {
    namespace = "com.jbnet.jbnet"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    // FA-002: Release signing via CI environment variables.
    // Set these four GitHub encrypted secrets before submitting to the Play Store:
    //   KEYSTORE_PATH             — path to the .jks file (uploaded as a secret file)
    //   KEYSTORE_STORE_PASSWORD   — store password
    //   KEYSTORE_KEY_ALIAS        — key alias
    //   KEYSTORE_KEY_PASSWORD     — key password
    //
    // Local dev (no env vars): falls back to the debug keystore so that
    // `flutter run --release` still works on a developer machine.
    signingConfigs {
        create("release") {
            val ksPath = System.getenv("KEYSTORE_PATH")
            if (!ksPath.isNullOrEmpty()) {
                storeFile = file(ksPath)
                storePassword = System.getenv("KEYSTORE_STORE_PASSWORD") ?: ""
                keyAlias = System.getenv("KEYSTORE_KEY_ALIAS") ?: ""
                keyPassword = System.getenv("KEYSTORE_KEY_PASSWORD") ?: ""
            } else {
                // Developer fallback — NOT used for Play Store builds.
                storeFile = signingConfigs.getByName("debug").storeFile
                storePassword = signingConfigs.getByName("debug").storePassword
                keyAlias = signingConfigs.getByName("debug").keyAlias
                keyPassword = signingConfigs.getByName("debug").keyPassword
            }
        }
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "com.jbnet.jbnet"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    buildTypes {
        release {
            signingConfig = signingConfigs.getByName("release")
        }
    }
}

flutter {
    source = "../.."
}
