import 'package:flutter/material.dart';

class ConnectionsScreen extends StatelessWidget {
  const ConnectionsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Network'),
        actions: [
          IconButton(icon: const Icon(Icons.person_add), onPressed: () {
            // TODO: search and add connections
          }),
        ],
      ),
      body: const Center(child: Text('Your network — to be implemented')),
    );
  }
}
