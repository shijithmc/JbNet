import 'package:flutter/material.dart';

/// Shown while [AuthStateNotifier] restores a previously saved Cognito session.
/// Typically visible for 100–300 ms on cold start for returning users. FA-005.
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(child: CircularProgressIndicator()),
    );
  }
}
