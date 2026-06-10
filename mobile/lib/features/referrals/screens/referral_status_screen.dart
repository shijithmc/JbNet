import 'package:flutter/material.dart';

class ReferralStatusScreen extends StatelessWidget {
  final String requestId;
  const ReferralStatusScreen({super.key, required this.requestId});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Referral Status')),
      body: Center(child: Text('Request $requestId — status tracking')),
    );
  }
}
