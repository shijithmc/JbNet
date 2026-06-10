import 'package:flutter/material.dart';

class JbNetTheme {
  JbNetTheme._();

  static const _seedColor = Color(0xFF1A73E8); // Google-blue — professional, trust-inspiring

  // Shared shape constants so light and dark themes stay in sync. FA-M09.
  static const _inputBorder  = OutlineInputBorder(
    borderRadius: BorderRadius.all(Radius.circular(8)),
  );
  static const _inputPadding = EdgeInsets.symmetric(horizontal: 16, vertical: 14);
  static const _buttonShape  = RoundedRectangleBorder(
    borderRadius: BorderRadius.all(Radius.circular(8)),
  );

  static final light = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(seedColor: _seedColor),
    appBarTheme: const AppBarTheme(centerTitle: false, elevation: 0),
    cardTheme: CardThemeData(
      elevation: 1,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    ),
    inputDecorationTheme: const InputDecorationTheme(
      border: _inputBorder,
      contentPadding: _inputPadding,
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: const Size.fromHeight(48),
        shape: _buttonShape,
      ),
    ),
  );

  static final dark = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
        seedColor: _seedColor, brightness: Brightness.dark),
    appBarTheme: const AppBarTheme(centerTitle: false, elevation: 0),
    cardTheme: CardThemeData(
      elevation: 1,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    ),
    // FA-M09: dark theme mirrors light — consistent input and button styling.
    inputDecorationTheme: const InputDecorationTheme(
      border: _inputBorder,
      contentPadding: _inputPadding,
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: const Size.fromHeight(48),
        shape: _buttonShape,
      ),
    ),
  );
}
